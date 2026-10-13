using System.Diagnostics;
using System.Text.Json;
using Dapr;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Observability;
using HexMaster.Battleship.Events.Routing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Api.Endpoints;

// POST /integration-events: the Dapr sidecar delivers every message of the integration-events
// topic here. Routes valid envelopes and acknowledges them, delivered or not.
public static class IntegrationEventEndpoints
{
    // Dapr redelivers on any non-2xx status except 404. A message that can never be valid is
    // acknowledged with this body instead, which makes the sidecar drop it with a warning.
    public static readonly TopicResponse Drop = new("DROP");

    public static IEndpointRouteBuilder MapIntegrationEventEndpoints(this IEndpointRouteBuilder app, PubSubOptions pubSub)
    {
        var group = app.MapGroup("/integration-events").WithTags("Integration events");

        group.MapPost("", Receive)
            .WithTopic(pubSub.Name, pubSub.Topic)
            .WithName("ReceiveIntegrationEvent")
            .Accepts<IntegrationEventEnvelope>("application/json")
            .Produces<TopicResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<Results<Ok, Ok<TopicResponse>>> Receive(
        HttpRequest request,
        IEventRouter router,
        EventsMetrics metrics,
        [Microsoft.AspNetCore.Mvc.FromServices] PubSubOptions pubSub,
        IOptions<JsonOptions> jsonOptions,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(IntegrationEventEndpoints));

        // The server span of this request continues the trace context the sidecar propagated
        // from the publisher; the consumer span beneath it covers the processing of the message.
        using var activity = EventsTelemetry.ActivitySource.StartActivity(
            EventsTelemetry.ProcessActivityName(pubSub.Topic), ActivityKind.Consumer);
        activity?.SetTag(EventsTelemetry.MessagingSystemAttribute, EventsTelemetry.MessagingSystem);
        activity?.SetTag(EventsTelemetry.DestinationNameAttribute, pubSub.Topic);
        activity?.SetTag(EventsTelemetry.OperationTypeAttribute, EventsTelemetry.ProcessOperation);
        activity?.SetTag(EventsTelemetry.OperationNameAttribute, EventsTelemetry.ProcessOperation);

        IntegrationEventEnvelope? envelope;
        try
        {
            envelope = await request.ReadFromJsonAsync<IntegrationEventEnvelope>(
                jsonOptions.Value.SerializerOptions, request.HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "The message is not a valid envelope.");
            activity?.AddException(exception);
            metrics.Dropped(DropReasons.InvalidEnvelope);
            EventsLog.UnreadableEnvelopeDropped(logger, exception);
            return TypedResults.Ok(Drop);
        }

        var errors = IntegrationEventEnvelopeValidator.Validate(envelope);
        if (errors.Count > 0)
        {
            var reasons = string.Join(" ", errors.Values.SelectMany(e => e));

            // The type of an invalid envelope is untrusted, so it is logged but never used as a tag.
            activity?.SetStatus(ActivityStatusCode.Error, $"Invalid envelope: {reasons}");
            metrics.Dropped(DropReasons.InvalidEnvelope);
            EventsLog.InvalidEnvelopeDropped(logger, envelope?.Id, envelope?.Type, reasons);
            return TypedResults.Ok(Drop);
        }

        activity?.SetTag(EventsTelemetry.MessageIdAttribute, envelope!.Id);
        activity?.SetTag(EventsTelemetry.EventTypeAttribute, envelope.Type);

        var result = router.Route(envelope);

        activity?.SetStatus(ActivityStatusCode.Ok);
        EventsLog.EventRouted(logger, envelope.Id, envelope.Type, result.Sequence, result.DeliveredTo);

        return TypedResults.Ok();
    }
}

public sealed record TopicResponse(string Status);
