using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Client;

// Typed client over /games on the gateway. The bearer token is added by BearerTokenHandler.
public sealed class GamesClient(HttpClient httpClient) : IGamesClient
{
    public Task<GameCallResult<JoinGameResponse>> JoinAsync(string code, CancellationToken cancellationToken) =>
        SendAsync<JoinGameResponse>(HttpMethod.Post, $"games/{Escape(code)}/join", null, cancellationToken);

    public Task<GameCallResult<GameView>> GetGameAsync(string code, CancellationToken cancellationToken) =>
        SendAsync<GameView>(HttpMethod.Get, $"games/{Escape(code)}", null, cancellationToken);

    public Task<GameCallResult<GameView>> PlaceShipAsync(string code, ShipPlacement placement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(placement);

        var kind = placement.Kind.ToString().ToLowerInvariant();
        var body = new PlaceShipRequest(placement.Origin.Column, placement.Origin.Row, placement.Orientation.ToString().ToLowerInvariant());
        return SendAsync<GameView>(HttpMethod.Put, $"games/{Escape(code)}/ships/{kind}", body, cancellationToken);
    }

    public Task<GameCallResult<GameView>> ReadyAsync(string code, CancellationToken cancellationToken) =>
        SendAsync<GameView>(HttpMethod.Post, $"games/{Escape(code)}/ready", null, cancellationToken);

    public Task<GameCallResult<FireShotResponse>> FireAsync(string code, Coordinate at, CancellationToken cancellationToken) =>
        SendAsync<FireShotResponse>(HttpMethod.Post, $"games/{Escape(code)}/shots", new FireShotRequest(at.Column, at.Row), cancellationToken);

    private static string Escape(string code) => Uri.EscapeDataString(code);

    private async Task<GameCallResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: BotJson.Options);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(BotJson.Options, cancellationToken);
            return value is null
                ? GameCallResult<T>.Problem(GameCallStatus.Failed, "The response had no body.")
                : GameCallResult<T>.Ok(value);
        }

        var (type, detail) = await ReadProblemAsync(response, cancellationToken);
        return GameCallResult<T>.Problem(MapStatus(response.StatusCode, type), detail);
    }

    internal static GameCallStatus MapStatus(HttpStatusCode statusCode, string? problemType)
    {
        var slug = problemType?[(problemType.LastIndexOf('/') + 1)..];

        return (statusCode, slug) switch
        {
            (HttpStatusCode.NotFound, _) => GameCallStatus.NotFound,
            (HttpStatusCode.Unauthorized, _) => GameCallStatus.Unauthorized,
            (HttpStatusCode.BadRequest, "placement-out-of-board" or "placement-overlap") => GameCallStatus.PlacementRefused,
            (HttpStatusCode.Conflict, "game-full") => GameCallStatus.GameFull,
            (HttpStatusCode.Conflict, "game-already-started") => GameCallStatus.GameAlreadyStarted,
            (HttpStatusCode.Conflict, "game-not-started") => GameCallStatus.GameNotStarted,
            (HttpStatusCode.Conflict, "not-your-turn") => GameCallStatus.NotYourTurn,
            (HttpStatusCode.Conflict, "cell-already-shot") => GameCallStatus.CellAlreadyShot,
            (HttpStatusCode.Conflict, "player-ready") => GameCallStatus.PlayerReady,
            (HttpStatusCode.Conflict, "fleet-incomplete") => GameCallStatus.FleetIncomplete,
            (HttpStatusCode.Conflict, "game-update-conflict") => GameCallStatus.UpdateConflict,
            _ => GameCallStatus.Failed,
        };
    }

    private static async Task<(string? Type, string? Detail)> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(BotJson.Options, cancellationToken);
            return (problem?.Type, problem?.Detail ?? problem?.Title ?? response.ReasonPhrase);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            return (null, response.ReasonPhrase);
        }
    }

    private sealed record ProblemBody(string? Type, string? Title, string? Detail);
}
