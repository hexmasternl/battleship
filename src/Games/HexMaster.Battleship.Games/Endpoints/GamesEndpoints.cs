using System.Security.Claims;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.CreateGame;
using HexMaster.Battleship.Games.Features.JoinGame;
using HexMaster.Battleship.Games.Services;
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

        group.MapPost("/{code}/join", JoinGame)
            .WithName("JoinGame")
            .WithSummary("Seats the caller as player two of the game, or confirms the caller already plays in it.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    // Takes no body: the creator is whoever the access token names, never what a request claims.
    private static async Task<Results<Created<CreateGameResponse>, ProblemHttpResult>> CreateGame(
        ClaimsPrincipal principal,
        ICommandHandler<CreateGameCommand, CreateGameResponse> handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayer(principal, out var playerId, out var playerName))
        {
            return UnidentifiedPlayer();
        }

        var game = await handler.Handle(new CreateGameCommand(playerId, playerName), cancellationToken);
        return TypedResults.Created($"/games/{game.GameCode}", game);
    }

    // Takes no body: the joining player is whoever the access token names. Codes are matched
    // case-insensitively, and a code that cannot exist is answered like an unknown one.
    private static async Task<Results<Ok<JoinGameResponse>, ProblemHttpResult>> JoinGame(
        string code,
        ClaimsPrincipal principal,
        ICommandHandler<JoinGameCommand, JoinGameResult> handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayer(principal, out var playerId, out var playerName))
        {
            return UnidentifiedPlayer();
        }

        var normalized = code.ToUpperInvariant();
        if (!GameCode.TryParse(normalized, out var gameCode))
        {
            return GameNotFound(normalized);
        }

        try
        {
            var result = await handler.Handle(new JoinGameCommand(gameCode, playerId, playerName), cancellationToken);
            return result.Game is { } game ? TypedResults.Ok(game) : GameNotFound(gameCode.Value);
        }
        catch (GameJoinRefusedException refused) when (refused.Reason == GameJoinRefusedReason.GameFull)
        {
            return Conflict(GameProblemTypes.GameFull, "The game already has two players.", refused.Message);
        }
        catch (GameJoinRefusedException refused)
        {
            return Conflict(GameProblemTypes.GameAlreadyStarted, "The game has already started.", refused.Message);
        }
        catch (GameUpdateConflictException conflict)
        {
            return Conflict(GameProblemTypes.GameUpdateConflict, "The game was changed by another request. Try again.", conflict.Message);
        }
    }

    private static bool TryGetPlayer(ClaimsPrincipal principal, out Guid playerId, out string playerName)
    {
        playerName = principal.FindFirstValue("name") ?? string.Empty;
        return Guid.TryParse(principal.FindFirstValue("sub"), out playerId) && !string.IsNullOrWhiteSpace(playerName);
    }

    private static ProblemHttpResult UnidentifiedPlayer() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "The access token does not identify a player.");

    private static ProblemHttpResult GameNotFound(string code) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: GameProblemTypes.GameNotFound,
            title: "The game was not found.",
            detail: $"No game has code {code}.");

    private static ProblemHttpResult Conflict(string type, string title, string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, type: type, title: title, detail: detail);
}
