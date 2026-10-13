using System.Text.Json;
using Dapr;
using HexMaster.Battleship.Aspire;
using HexMaster.Battleship.DevBot.Events;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.DevBot.Endpoints;

// POST /integration-events: the bot's Dapr sidecar (app id dev-bot) delivers every message of the
// integration-events topic here, from the bot's own queue. Every delivery is acknowledged; a
// failure to act is caught up by the reconcile timer instead of a redelivery.
public static class IntegrationEventEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/integration-events", Receive)
            .WithTopic(AspireConstants.PubSubName, AspireConstants.IntegrationEventsTopic)
            .WithName("ReceiveIntegrationEvent")
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> Receive(
        HttpRequest request,
        IntegrationEventHandler handler,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        IntegrationEventEnvelope? envelope;
        try
        {
            envelope = await request.ReadFromJsonAsync<IntegrationEventEnvelope>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return Results.Ok();
        }

        if (envelope is null || IntegrationEventEnvelopeValidator.Validate(envelope).Count > 0)
        {
            return Results.Ok();
        }

        try
        {
            await handler.HandleAsync(envelope, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(typeof(IntegrationEventEndpoints))
                .LogWarning(exception, "Could not handle envelope {EnvelopeId} ({EventType}).", envelope.Id, envelope.Type);
        }

        return Results.Ok();
    }
}
