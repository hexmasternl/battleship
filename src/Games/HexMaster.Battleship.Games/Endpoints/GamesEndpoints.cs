using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.CreateGame;
using HexMaster.Battleship.Games.Features.FireShot;
using HexMaster.Battleship.Games.Features.GetGame;
using HexMaster.Battleship.Games.Features.JoinGame;
using HexMaster.Battleship.Games.Features.MarkReady;
using HexMaster.Battleship.Games.Features.PlaceShip;
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

        group.MapGet("/{code}", GetGame)
            .WithName("GetGame")
            .WithSummary("Returns the caller's view of the game: the caller with ships, the opponent without.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{code}/ships/{kind}", PlaceShip)
            .WithName("PlaceShip")
            .WithSummary("Places or moves one of the caller's ships and returns the caller's view of the game.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{code}/ready", MarkReady)
            .WithName("MarkReady")
            .WithSummary("Marks the caller ready once all five ships are placed, starting the game when both players are.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{code}/shots", FireShot)
            .WithName("FireShot")
            .WithSummary("Fires the caller's shot at a cell of the opponent's board and returns whether it hit.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    // The shooter is whoever the access token names. A cell off the 12x12 board is a validation
    // problem; the game rules (started, turn, repeated cell) answer 409 with their own problem type.
    private static async Task<Results<Ok<FireShotResponse>, ValidationProblem, ProblemHttpResult>> FireShot(
        string code,
        FireShotRequest request,
        ClaimsPrincipal principal,
        ICommandHandler<FireShotCommand, FireShotResult> handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(principal, out var playerId))
        {
            return UnidentifiedPlayer();
        }

        var normalized = code.ToUpperInvariant();
        if (!GameCode.TryParse(normalized, out var gameCode))
        {
            return GameNotFound(normalized);
        }

        if (!TryReadCell(request, out var at, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var result = await handler.Handle(new FireShotCommand(gameCode, playerId, at), cancellationToken);
            return result.Shot is { } shot ? TypedResults.Ok(shot) : GameNotFound(gameCode.Value);
        }
        catch (ShotRefusedException refused)
        {
            return refused.Reason switch
            {
                ShotRefusedReason.NotYourTurn =>
                    Conflict(GameProblemTypes.NotYourTurn, "It is not your turn.", refused.Message),
                ShotRefusedReason.CellAlreadyShot =>
                    Conflict(GameProblemTypes.CellAlreadyShot, "You already fired at that cell.", refused.Message),
                _ => Conflict(GameProblemTypes.GameNotStarted, "The game is not being played.", refused.Message),
            };
        }
        catch (GameUpdateConflictException conflict)
        {
            return Conflict(GameProblemTypes.GameUpdateConflict, "The game was changed by another request. Try again.", conflict.Message);
        }
    }

    private static bool TryReadCell(FireShotRequest? request, out Coordinate at, out Dictionary<string, string[]> errors)
    {
        errors = [];
        at = default;

        if (request?.Column is not { } column)
        {
            errors["column"] = ["The column is required."];
        }
        else if (column is < 0 or >= Board.Size)
        {
            errors["column"] = [$"The column must be from 0 to {Board.Size - 1}."];
        }

        if (request?.Row is not { } row)
        {
            errors["row"] = ["The row is required."];
        }
        else if (row is < 0 or >= Board.Size)
        {
            errors["row"] = [$"The row must be from 0 to {Board.Size - 1}."];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        at = new Coordinate(request!.Column!.Value, request.Row!.Value);
        return true;
    }

    // Takes no body. Getting ready again answers 200 with the same view, in any game state.
    private static async Task<Results<Ok<GameView>, ProblemHttpResult>> MarkReady(
        string code,
        ClaimsPrincipal principal,
        ICommandHandler<MarkReadyCommand, GameView?> handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(principal, out var playerId))
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
            var view = await handler.Handle(new MarkReadyCommand(gameCode, playerId), cancellationToken);
            return view is null ? GameNotFound(gameCode.Value) : TypedResults.Ok(view);
        }
        catch (PlacementRefusedException refused)
        {
            return PlacementRefused(refused);
        }
        catch (GameUpdateConflictException conflict)
        {
            return Conflict(GameProblemTypes.GameUpdateConflict, "The game was changed by another request. Try again.", conflict.Message);
        }
    }

    // The kind comes from the route and the orientation from the body, both in any casing.
    private static async Task<Results<Ok<GameView>, ValidationProblem, ProblemHttpResult>> PlaceShip(
        string code,
        string kind,
        PlaceShipRequest request,
        ClaimsPrincipal principal,
        ICommandHandler<PlaceShipCommand, GameView?> handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(principal, out var playerId))
        {
            return UnidentifiedPlayer();
        }

        var normalized = code.ToUpperInvariant();
        if (!GameCode.TryParse(normalized, out var gameCode))
        {
            return GameNotFound(normalized);
        }

        if (!TryReadPlacement(kind, request, out var placement, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var view = await handler.Handle(new PlaceShipCommand(gameCode, playerId, placement), cancellationToken);
            return view is null ? GameNotFound(gameCode.Value) : TypedResults.Ok(view);
        }
        catch (PlacementRefusedException refused)
        {
            return PlacementRefused(refused);
        }
        catch (GameUpdateConflictException conflict)
        {
            return Conflict(GameProblemTypes.GameUpdateConflict, "The game was changed by another request. Try again.", conflict.Message);
        }
    }

    private static bool TryReadPlacement(
        string kind,
        PlaceShipRequest? request,
        [NotNullWhen(true)] out ShipPlacement? placement,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        placement = null;

        if (!Enum.TryParse<ShipKind>(kind, ignoreCase: true, out var shipKind) || !Enum.IsDefined(shipKind) || IsNumeric(kind))
        {
            errors["kind"] = [$"'{kind}' is not a ship kind; use one of {string.Join(", ", Fleet.Kinds).ToLowerInvariant()}."];
        }

        if (request?.Column is null)
        {
            errors["column"] = ["The column is required."];
        }

        if (request?.Row is null)
        {
            errors["row"] = ["The row is required."];
        }

        if (request?.Orientation is not { } text
            || !Enum.TryParse<Orientation>(text, ignoreCase: true, out var orientation)
            || !Enum.IsDefined(orientation)
            || IsNumeric(text))
        {
            errors["orientation"] = ["The orientation must be horizontal or vertical."];
            return false;
        }

        if (errors.Count > 0)
        {
            return false;
        }

        placement = new ShipPlacement(shipKind, new Coordinate(request.Column!.Value, request.Row!.Value), orientation);
        return true;
    }

    // Enum.TryParse also accepts "0" or "3"; only names are part of the contract.
    private static bool IsNumeric(string value) => value.Length > 0 && value.All(c => char.IsAsciiDigit(c) || c == '-');

    private static ProblemHttpResult PlacementRefused(PlacementRefusedException refused) => refused.Reason switch
    {
        PlacementRefusedReason.OutOfBoard => TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest, type: GameProblemTypes.PlacementOutOfBoard,
            title: "The ship does not fit on the board there.", detail: refused.Message),
        PlacementRefusedReason.Overlap => TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest, type: GameProblemTypes.PlacementOverlap,
            title: "The ship would overlap another ship.", detail: refused.Message),
        PlacementRefusedReason.PlayerReady =>
            Conflict(GameProblemTypes.PlayerReady, "Your fleet is ready and can no longer change.", refused.Message),
        PlacementRefusedReason.FleetIncomplete =>
            Conflict(GameProblemTypes.FleetIncomplete, "Place every ship before getting ready.", refused.Message),
        _ => Conflict(GameProblemTypes.GameAlreadyStarted, "The game has already started.", refused.Message),
    };

    // A game the caller does not play in is answered like an unknown one, so codes cannot be probed.
    private static async Task<Results<Ok<GameView>, ProblemHttpResult>> GetGame(
        string code,
        ClaimsPrincipal principal,
        IQueryHandler<GetGameQuery, GameView?> handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(principal, out var playerId))
        {
            return UnidentifiedPlayer();
        }

        var normalized = code.ToUpperInvariant();
        if (!GameCode.TryParse(normalized, out var gameCode))
        {
            return GameNotFound(normalized);
        }

        var view = await handler.Handle(new GetGameQuery(gameCode, playerId), cancellationToken);
        return view is null ? GameNotFound(gameCode.Value) : TypedResults.Ok(view);
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

    private static bool TryGetPlayerId(ClaimsPrincipal principal, out Guid playerId) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out playerId);

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
