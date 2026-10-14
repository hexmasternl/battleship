using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Features.CleanUpInactiveGames;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace HexMaster.Battleship.Games.Endpoints;

// /maintenance: internal operations triggered by the platform, here the daily cleanup that the
// Dapr cron binding on the sidecar posts to. No authorization, like the sidecar's other routes:
// the gateway reserves the prefix and never routes it, so only the sidecar's network reaches it.
public static class MaintenanceEndpoints
{
    public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/maintenance").WithTags("Maintenance");

        group.MapPost("/game-cleanup", CleanUpInactiveGames)
            .WithName("CleanUpInactiveGames")
            .WithSummary("Removes every game without activity for longer than the retention period and reports the counts.");

        return app;
    }

    // Ignores any body: the cron binding posts its own payload, and the run takes no input.
    private static async Task<Ok<CleanUpInactiveGamesResult>> CleanUpInactiveGames(
        ICommandHandler<CleanUpInactiveGamesCommand, CleanUpInactiveGamesResult> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new CleanUpInactiveGamesCommand(), cancellationToken));
}
