using System.Security.Claims;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Features.CreateGame;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace HexMaster.Battleship.Games.Endpoints;

// /games: creating and, later, playing games. Every endpoint here requires a bearer access token.
public static class GamesEndpoints
{
    public static IEndpointRouteBuilder MapGamesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/games").WithTags("Games").RequireAuthorization();

        group.MapPost("", CreateGame)
            .WithName("CreateGame")
            .WithSummary("Creates a new game with the caller as player one and returns its id and code.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    // Takes no body: the creator is whoever the access token names, never what a request claims.
    private static async Task<Results<Created<CreateGameResponse>, ProblemHttpResult>> CreateGame(
        ClaimsPrincipal principal,
        ICommandHandler<CreateGameCommand, CreateGameResponse> handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var playerId)
            || principal.FindFirstValue("name") is not { } playerName
            || string.IsNullOrWhiteSpace(playerName))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "The access token does not identify a player.");
        }

        var game = await handler.Handle(new CreateGameCommand(playerId, playerName), cancellationToken);
        return TypedResults.Created($"/games/{game.GameCode}", game);
    }
}
