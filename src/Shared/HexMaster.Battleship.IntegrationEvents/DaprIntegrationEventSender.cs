using System.Diagnostics;
using System.Text.Json;
using Dapr.Client;
using HexMaster.Battleship.Aspire;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.IntegrationEvents.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.IntegrationEvents;

// Builds the envelope, checks it with the same validator the Events service applies, and
// publishes it through the service's Dapr sidecar. An envelope this sender publishes is never
// one the Events service drops. Each publish runs in a producer span, so the trace continues
// through the sidecar to the Events service.
public sealed class DaprIntegrationEventSender(
    DaprClient daprClient,
    TimeProvider timeProvider,
    IntegrationEventsMetrics metrics,
    ILogger<DaprIntegrationEventSender> logger) : IIntegrationEventSender
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync<TEvent>(TEvent integrationEvent, EventAudience audience, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        const string topic = AspireConstants.IntegrationEventsTopic;

        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid().ToString(),
            TEvent.EventType,
            audience.Target,
            audience.TargetIds,
            timeProvider.GetUtcNow(),
            JsonSerializer.SerializeToElement(integrationEvent, PayloadOptions));

        using var activity = IntegrationEventsTelemetry.ActivitySource.StartActivity(
            IntegrationEventsTelemetry.PublishActivityName(topic), ActivityKind.Producer);
        activity?.SetTag(IntegrationEventsTelemetry.MessagingSystemAttribute, IntegrationEventsTelemetry.MessagingSystem);
        activity?.SetTag(IntegrationEventsTelemetry.DestinationNameAttribute, topic);
        activity?.SetTag(IntegrationEventsTelemetry.OperationTypeAttribute, IntegrationEventsTelemetry.PublishOperation);
        activity?.SetTag(IntegrationEventsTelemetry.OperationNameAttribute, IntegrationEventsTelemetry.PublishOperation);
        activity?.SetTag(IntegrationEventsTelemetry.MessageIdAttribute, envelope.Id);
        activity?.SetTag(IntegrationEventsTelemetry.EventTypeAttribute, envelope.Type);

        try
        {
            var errors = IntegrationEventEnvelopeValidator.Validate(envelope);
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    $"Cannot publish {TEvent.EventType}: {string.Join(" ", errors.Values.SelectMany(e => e))}",
                    nameof(audience));
            }

            await daprClient.PublishEventAsync(
                AspireConstants.PubSubName,
                topic,
                envelope,
                cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            metrics.PublishFailed(envelope.Type);
            IntegrationEventsLog.PublishFailed(logger, exception, envelope.Id, envelope.Type, topic);

            throw;
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        metrics.Published(envelope.Type);
        IntegrationEventsLog.Published(logger, envelope.Id, envelope.Type, topic);
    }
}
