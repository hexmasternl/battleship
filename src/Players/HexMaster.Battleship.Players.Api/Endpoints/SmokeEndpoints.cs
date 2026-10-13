using Dapr.Client;

namespace HexMaster.Battleship.Players.Api.Endpoints;

// Throwaway development-only endpoints that prove the messaging backbone end to end:
// the Players API publishes to topic "smoke" on the shared "pubsub" component and the
// Games API subscribes to it. Removed by the change that introduces the first real
// integration event.
public static class SmokeEndpoints
{
    public const string PubSubName = "pubsub";
    public const string TopicName = "smoke";

    public static IEndpointRouteBuilder MapSmokeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/smoke").WithTags("Smoke");

        group.MapPost("/publish", async (DaprClient dapr, CancellationToken cancellationToken) =>
        {
            var smokeEvent = new SmokeEvent(Guid.NewGuid());

            await dapr.PublishEventAsync(PubSubName, TopicName, smokeEvent, cancellationToken);

            return Results.Accepted(value: smokeEvent);
        })
        .WithName("PublishSmokeEvent")
        .Produces<SmokeEvent>(StatusCodes.Status202Accepted);

        return app;
    }
}

public sealed record SmokeEvent(Guid Id);
