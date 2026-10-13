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

    [Fact]
    public async Task PlaceShip_WithoutAToken_Is401AndChangesNothing()
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(game.GameCode, "carrier", token: null, Body(0, 0));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty((await ReadAsync(game)).PlayerOne.Ships);
    }

    [Fact]
    public async Task PlaceShip_UnknownGame_Is404()
    {
        using var response = await PlaceAsync(GameFactory.Code().Value, "carrier", Token(Guid.NewGuid()), Body(0, 0));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
    }

    [Fact]
    public async Task PlaceShip_ForSomeoneWhoDoesNotPlay_Is404()
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(game.GameCode, "carrier", Token(Guid.NewGuid()), Body(0, 0));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
    }

    [Theory]
    [InlineData("carrier", "horizontal")]
    [InlineData("Carrier", "Vertical")]
    [InlineData("CARRIER", "HORIZONTAL")]
    public async Task PlaceShip_Is200WithTheViewForAnyCasing(string kind, string orientation)
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(
            game.GameCode.ToLowerInvariant(), kind, Token(game.PlayerOne.PlayerId), Body(2, 3, orientation));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ship = Assert.Single(JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("me").GetProperty("ships").EnumerateArray());
        Assert.Equal("carrier", ship.GetProperty("kind").GetString());
        Assert.Equal(orientation.ToLowerInvariant(), ship.GetProperty("orientation").GetString());
        var stored = Assert.Single((await ReadAsync(game)).PlayerOne.Ships);
        Assert.Equal(new Coordinate(2, 3), stored.Origin);
    }

    [Theory]
    [InlineData("frigate")]
    [InlineData("0")]
    public async Task PlaceShip_UnknownKind_Is400Validation(string kind)
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(game.GameCode, kind, Token(game.PlayerOne.PlayerId), Body(0, 0));

        await AssertValidationAsync(response, "kind");
    }

    [Theory]
    [InlineData("diagonal")]
    [InlineData("1")]
    [InlineData(null)]
    public async Task PlaceShip_UnknownOrMissingOrientation_Is400Validation(string? orientation)
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(
            game.GameCode, "carrier", Token(game.PlayerOne.PlayerId), new { column = 0, row = 0, orientation });

        await AssertValidationAsync(response, "orientation");
    }

    [Fact]
    public async Task PlaceShip_MissingColumnOrRow_Is400Validation()
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(
            game.GameCode, "carrier", Token(game.PlayerOne.PlayerId), new { orientation = "horizontal" });

        await AssertValidationAsync(response, "column", "row");
    }

    [Fact]
    public async Task PlaceShip_NonIntegerColumn_Is400()
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(
            game.GameCode, "carrier", Token(game.PlayerOne.PlayerId), new { column = "A", row = 0, orientation = "horizontal" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await ReadAsync(game)).PlayerOne.Ships);
    }

    [Fact]
    public async Task PlaceShip_OffTheBoard_Is400PlacementOutOfBoard()
    {
        var game = await StoreAsync();

        using var response = await PlaceAsync(game.GameCode, "carrier", Token(game.PlayerOne.PlayerId), Body(8, 0));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "placement-out-of-board");
    }

    [Fact]
    public async Task PlaceShip_Overlapping_Is400PlacementOverlap()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New,
            playerOne: GameFactory.Player(ships: [new ShipPlacement(ShipKind.Carrier, new(0, 0), Orientation.Horizontal)])));

        using var response = await PlaceAsync(
            game.GameCode, "destroyer", Token(game.PlayerOne.PlayerId), Body(4, 0, "vertical"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "placement-overlap");
    }

    [Fact]
    public async Task PlaceShip_WhenReady_Is409PlayerReady()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New,
            playerOne: GameFactory.Player(isReady: true, ships: GameFactory.FullFleet())));

        using var response = await PlaceAsync(game.GameCode, "carrier", Token(game.PlayerOne.PlayerId), Body(0, 11));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "player-ready");
    }

    [Fact]
    public async Task PlaceShip_InAStartedGame_Is409GameAlreadyStarted()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.Started));

        using var response = await PlaceAsync(game.GameCode, "carrier", Token(game.PlayerOne.PlayerId), Body(0, 0));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "game-already-started");
    }

    private static object Body(int column, int row, string orientation = "horizontal") => new { column, row, orientation };

    private Task<HttpResponseMessage> PlaceAsync(string code, string kind, string? token, object body) =>
        SendAsync(HttpMethod.Put, $"/games/{code}/ships/{kind}", token, body);

    private static async Task AssertValidationAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.All(fields, field => Assert.Contains(field, problem!.Errors.Keys));
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
