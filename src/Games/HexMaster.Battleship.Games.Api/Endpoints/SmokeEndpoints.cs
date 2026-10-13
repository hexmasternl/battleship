using Dapr;

namespace HexMaster.Battleship.Games.Api.Endpoints;

// Throwaway development-only endpoints that prove the messaging backbone end to end:
// the Players API publishes to topic "smoke" on the shared "pubsub" component, the
// sidecar delivers each event to POST /smoke/received here, and GET /smoke/received/{id}
// tells whether a given event arrived. Removed by the change that introduces the first
// real integration event.
public static class SmokeEndpoints
{
    public const string PubSubName = "pubsub";
    public const string TopicName = "smoke";

    public static IEndpointRouteBuilder MapSmokeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/smoke").WithTags("Smoke");

        group.MapPost("/received", [Topic(PubSubName, TopicName)] (SmokeEvent smokeEvent, SmokeEventStore store) =>
        {
            store.Record(smokeEvent.Id);

            return Results.Ok();
        })
        .WithName("ReceiveSmokeEvent")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/received/{id:guid}", (Guid id, SmokeEventStore store) =>
            store.Contains(id) ? Results.Ok() : Results.NotFound())
        .WithName("GetReceivedSmokeEvent")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record SmokeEvent(Guid Id);

// In-memory record of the smoke events this instance has received.
public sealed class SmokeEventStore
{
    private readonly HashSet<Guid> _received = [];
    private readonly Lock _lock = new();

    public void Record(Guid id)
    {
        lock (_lock)
        {
            _received.Add(id);
        }
    }

    public bool Contains(Guid id)
    {
        lock (_lock)
        {
            return _received.Contains(id);
        }
    }
}
