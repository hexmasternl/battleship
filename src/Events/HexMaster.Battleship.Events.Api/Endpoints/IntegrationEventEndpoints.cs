using System.Text.Json;
using Dapr;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
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
        IOptions<JsonOptions> jsonOptions,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(IntegrationEventEndpoints));

        IntegrationEventEnvelope? envelope;
        try
        {
            envelope = await request.ReadFromJsonAsync<IntegrationEventEnvelope>(
                jsonOptions.Value.SerializerOptions, request.HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Dropped an integration event that is not a valid envelope");
            return TypedResults.Ok(Drop);
        }

        var errors = IntegrationEventEnvelopeValidator.Validate(envelope);
        if (errors.Count > 0)
        {
            logger.LogWarning(
                "Dropped invalid integration event {EventId} of type {EventType}: {Errors}",
                envelope?.Id, envelope?.Type, string.Join(" ", errors.Values.SelectMany(e => e)));
            return TypedResults.Ok(Drop);
        }

        var result = router.Route(envelope!);

        logger.LogInformation(
            "Routed integration event {EventId} of type {EventType} as {Sequence} to {ConnectionCount} connections",
            envelope!.Id, envelope.Type, result.Sequence, result.DeliveredTo);

        return TypedResults.Ok();
    }
}

public sealed record TopicResponse(string Status);
