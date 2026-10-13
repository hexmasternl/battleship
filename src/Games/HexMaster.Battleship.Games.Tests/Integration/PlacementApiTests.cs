using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Tests.Factories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Integration;

// GET /games/{code}, PUT /games/{code}/ships/{kind} and POST /games/{code}/ready against the running
// API with an in-memory store that keeps games between requests.
public class PlacementApiTests : IClassFixture<JoinGameApiFactory>
{
    private const string Problems = "https://battleship.hexmaster.nl/problems/";

    private readonly JoinGameApiFactory _factory;

    public PlacementApiTests(JoinGameApiFactory factory)
    {
        _factory = factory;
        _factory.Events.Reset();
    }

    [Fact]
    public async Task GetGame_WithoutAToken_Is401()
    {
        var game = await StoreAsync();

        using var response = await SendAsync(HttpMethod.Get, $"/games/{game.GameCode}", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGame_UnknownCode_Is404GameNotFound()
    {
        using var response = await SendAsync(HttpMethod.Get, $"/games/{GameFactory.Code()}", Token(Guid.NewGuid()));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
    }

    [Fact]
    public async Task GetGame_MalformedCode_Is404GameNotFound()
    {
        using var response = await SendAsync(HttpMethod.Get, "/games/ABC-123", Token(Guid.NewGuid()));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
    }

    [Fact]
    public async Task GetGame_ForSomeoneWhoDoesNotPlay_Is404GameNotFound()
    {
        var game = await StoreAsync();

        using var response = await SendAsync(HttpMethod.Get, $"/games/{game.GameCode}", Token(Guid.NewGuid()));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
    }

    [Fact]
    public async Task GetGame_LowerCaseCode_Is200WithTheCallersViewAndNoOpponentShips()
    {
        var playerOne = GameFactory.Player(isReady: true, ships: GameFactory.FullFleet());
        var playerTwo = GameFactory.Player(
            ships: [new ShipPlacement(ShipKind.Destroyer, new(10, 11), Orientation.Horizontal)]);
        var game = await StoreAsync(GameFactory.Stored(GameState.New, playerOne, playerTwo));

        using var response = await SendAsync(
            HttpMethod.Get, $"/games/{game.GameCode.ToLowerInvariant()}", Token(playerTwo.PlayerId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(json).RootElement;
        Assert.Equal(game.GameCode, body.GetProperty("gameCode").GetString());
        Assert.Equal("New", body.GetProperty("state").GetString());
        var me = body.GetProperty("me");
        Assert.Equal(playerTwo.PlayerId, me.GetProperty("playerId").GetGuid());
        Assert.False(me.GetProperty("ready").GetBoolean());
        var ship = Assert.Single(me.GetProperty("ships").EnumerateArray());
        Assert.Equal("destroyer", ship.GetProperty("kind").GetString());
        Assert.Equal(10, ship.GetProperty("column").GetInt32());
        Assert.Equal(11, ship.GetProperty("row").GetInt32());
        Assert.Equal("horizontal", ship.GetProperty("orientation").GetString());
        var opponent = body.GetProperty("opponent");
        Assert.Equal(playerOne.PlayerId, opponent.GetProperty("playerId").GetGuid());
        Assert.True(opponent.GetProperty("ready").GetBoolean());
        Assert.False(opponent.TryGetProperty("ships", out _));
        Assert.DoesNotContain("carrier", json, StringComparison.Ordinal);
        Assert.DoesNotContain("battleship\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetGame_ByTheCreatorAlone_HasANullOpponent()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New, withoutPlayerTwo: true));

        using var response = await SendAsync(HttpMethod.Get, $"/games/{game.GameCode}", Token(game.PlayerOne.PlayerId));

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(JsonValueKind.Null, body.GetProperty("opponent").ValueKind);
    }

    private static string Token(Guid playerId) => TestTokens.Issue(playerId, GameFactory.PlayerName());

    private async Task<Game> StoreAsync(Game? game = null)
    {
        game ??= GameFactory.Stored(GameState.New);
        await _factory.Games.TryAddAsync(game, CancellationToken.None);
        return game;
    }

    private async Task<IGame> ReadAsync(IGame game) =>
        (await _factory.Games.GetByCodeAsync(game.GameCode, CancellationToken.None))!;

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string slug)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Problems + slug, problem!.Type);
    }
}
