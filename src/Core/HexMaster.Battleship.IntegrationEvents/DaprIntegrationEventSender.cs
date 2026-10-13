using System.Text.Json;
using Dapr.Client;
using HexMaster.Battleship.Aspire;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.IntegrationEvents;

// Builds the envelope, checks it with the same validator the Events service applies, and
// publishes it through the service's Dapr sidecar. An envelope this sender publishes is never
// one the Events service drops.
public sealed class DaprIntegrationEventSender(DaprClient daprClient, TimeProvider timeProvider) : IIntegrationEventSender
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync<TEvent>(TEvent integrationEvent, EventAudience audience, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid().ToString(),
            TEvent.EventType,
            audience.Target,
            audience.TargetIds,
            timeProvider.GetUtcNow(),
            JsonSerializer.SerializeToElement(integrationEvent, PayloadOptions));

        var errors = IntegrationEventEnvelopeValidator.Validate(envelope);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                $"Cannot publish {TEvent.EventType}: {string.Join(" ", errors.Values.SelectMany(e => e))}",
                nameof(audience));
        }

        await daprClient.PublishEventAsync(
            AspireConstants.PubSubName,
            AspireConstants.IntegrationEventsTopic,
            envelope,
            cancellationToken);
    }
}
