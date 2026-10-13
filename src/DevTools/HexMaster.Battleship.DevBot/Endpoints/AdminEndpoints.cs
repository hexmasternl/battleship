using HexMaster.Battleship.DevBot.Admin;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Sessions;
using HexMaster.Battleship.DevBot.Strategies;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HexMaster.Battleship.DevBot.Endpoints;

// /admin: what the dashboard commands change and what the resource properties show.
public static class AdminEndpoints
{
    public const string StatePath = "/admin/state";

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin").ExcludeFromDescription();

        group.MapGet("/state", GetState).WithName("GetBotState");
        group.MapPut("/intelligence", SetIntelligence).WithName("SetBotIntelligence");
        group.MapPut("/auto-join", SetAutoJoin).WithName("SetBotAutoJoin");
        group.MapPost("/games/{code}", JoinGame).WithName("JoinBotGame");

        return app;
    }

    public static string PresetList { get; } = string.Join(", ", Enum.GetNames<BotIntelligence>());

    private static Ok<AdminStateResponse> GetState(BotSettings settings, GameSessions sessions, TokenStore tokenStore) =>
        TypedResults.Ok(State(settings, sessions, tokenStore));

    private static Results<Ok<AdminStateResponse>, ProblemHttpResult> SetIntelligence(
        SetIntelligenceRequest? request,
        BotSettings settings,
        GameSessions sessions,
        TokenStore tokenStore)
    {
        var name = request?.Intelligence?.Trim();
        if (string.IsNullOrEmpty(name)
            || name.Any(char.IsAsciiDigit)
            || !Enum.TryParse<BotIntelligence>(name, ignoreCase: true, out var intelligence)
            || !Enum.IsDefined(intelligence))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown intelligence preset.",
                detail: $"'{name}' is not a preset; use one of {PresetList}.");
        }

        settings.Intelligence = intelligence;
        return TypedResults.Ok(State(settings, sessions, tokenStore));
    }

    private static Results<Ok<AdminStateResponse>, ProblemHttpResult> SetAutoJoin(
        SetAutoJoinRequest? request,
        BotSettings settings,
        GameSessions sessions,
        TokenStore tokenStore)
    {
        if (request?.Enabled is not { } enabled)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Auto-join setting missing.",
                detail: "Send { \"enabled\": true } or { \"enabled\": false }.");
        }

        settings.AutoJoin = enabled;
        return TypedResults.Ok(State(settings, sessions, tokenStore));
    }

    // Joins regardless of auto-join and the grace period. A game the bot already plays in answers 200
    // from the Games API, so this also resumes a game after a restart.
    private static async Task<Results<Ok<JoinGameAdminResponse>, ProblemHttpResult>> JoinGame(
        string code,
        GameSessions sessions,
        CancellationToken cancellationToken)
    {
        var result = await sessions.JoinAsync(code, cancellationToken);
        if (result.IsOk)
        {
            sessions.TriggerReconcile(result.Value.GameCode);
            return TypedResults.Ok(new JoinGameAdminResponse(result.Value.GameCode));
        }

        var normalized = code.Trim().ToUpperInvariant();
        return result.Status switch
        {
            GameCallStatus.NotFound => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, title: "Game not found.",
                detail: $"No game has code {normalized}."),
            GameCallStatus.GameFull => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Game full.",
                detail: $"Game {normalized} is full: it already has two other players."),
            GameCallStatus.GameAlreadyStarted => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Game already started.",
                detail: $"Game {normalized} has already started without the bot."),
            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway, title: "Join failed.",
                detail: $"The Games API refused to seat the bot in {normalized} ({result.Status}): {result.Detail}"),
        };
    }

    private static AdminStateResponse State(BotSettings settings, GameSessions sessions, TokenStore tokenStore) =>
        new(settings.Intelligence.ToString(), settings.AutoJoin, sessions.ActiveCodes, tokenStore.CurrentUserId);
}
