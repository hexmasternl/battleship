using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Integration;

// POST /games/{code}/shots against the running API with an in-memory store that keeps games between
// requests and honours concurrency tokens.
public class FireShotApiTests : IClassFixture<JoinGameApiFactory>
{
    private const string Problems = "https://battleship.hexmaster.nl/problems/";

    private readonly JoinGameApiFactory _factory;

    public FireShotApiTests(JoinGameApiFactory factory)
    {
        _factory = factory;
        _factory.Events.Reset();
        _factory.Games.AfterRead = null;
    }

    [Fact]
    public async Task FireShot_WithoutAToken_Is401AndChangesNothing()
    {
        var game = await StoreAsync();

        using var response = await FireAsync(game.GameCode, token: null, Body(3, 7));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty((await ReadAsync(game)).PlayerOne.Shots);
    }

    [Fact]
    public async Task FireShot_UnknownCode_Is404()
    {
        using var response = await FireAsync(GameFactory.Code().Value, Token(Guid.NewGuid()), Body(3, 7));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
    }

    [Fact]
    public async Task FireShot_MalformedCode_Is404()
    {
        using var response = await FireAsync("ABC-123", Token(Guid.NewGuid()), Body(3, 7));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
    }

    [Fact]
    public async Task FireShot_BySomeoneWhoDoesNotPlay_Is404AndChangesNothing()
    {
        var game = await StoreAsync();
        var token = _factory.Games.TokenOf(game.GameCode);

        using var response = await FireAsync(game.GameCode, Token(Guid.NewGuid()), Body(3, 7));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "game-not-found");
        Assert.Equal(token, _factory.Games.TokenOf(game.GameCode));
    }

    [Fact]
    public async Task FireShot_MissingColumnAndRow_Is400Validation()
    {
        var game = await StoreAsync();

        using var response = await FireAsync(game.GameCode, Token(game.PlayerOne.PlayerId), new { });

        await AssertValidationAsync(response, "column", "row");
    }

    [Theory]
    [InlineData(12, 3, "column")]
    [InlineData(-1, 3, "column")]
    [InlineData(3, 12, "row")]
    public async Task FireShot_OffTheBoard_Is400ValidationAndKeepsTheTurn(int column, int row, string field)
    {
        var game = await StoreAsync();

        using var response = await FireAsync(game.GameCode, Token(game.PlayerOne.PlayerId), Body(column, row));

        await AssertValidationAsync(response, field);
        var stored = await ReadAsync(game);
        Assert.Empty(stored.PlayerOne.Shots);
        Assert.True(stored.PlayerOne.HasTurn);
    }

    [Fact]
    public async Task FireShot_NonIntegerColumn_Is400()
    {
        var game = await StoreAsync();

        using var response = await FireAsync(game.GameCode, Token(game.PlayerOne.PlayerId), new { column = "A", row = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await ReadAsync(game)).PlayerOne.Shots);
    }

    [Fact]
    public async Task FireShot_InANewGame_Is409GameNotStarted()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New));

        using var response = await FireAsync(game.GameCode, Token(game.PlayerOne.PlayerId), Body(3, 7));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "game-not-started");
    }

    [Fact]
    public async Task FireShot_WithoutTheTurn_Is409NotYourTurn()
    {
        var game = await StoreAsync();

        using var response = await FireAsync(game.GameCode, Token(game.PlayerTwo!.PlayerId), Body(3, 7));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "not-your-turn");
        Assert.Empty((await ReadAsync(game)).PlayerTwo!.Shots);
    }

    [Fact]
    public async Task FireShot_AtARepeatedCell_Is409CellAlreadyShotAndKeepsTheTurn()
    {
        var game = await StoreAsync(GameFactory.Stored(
            GameState.Started,
            GameFactory.Player(hasTurn: true, ships: GameFactory.FullFleet(), shots: [new Shot(3, 7, false)]),
            GameFactory.Player(ships: GameFactory.FullFleet())));

        using var response = await FireAsync(game.GameCode, Token(game.PlayerOne.PlayerId), Body(3, 7));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "cell-already-shot");
        Assert.True((await ReadAsync(game)).PlayerOne.HasTurn);
    }

    [Fact]
    public async Task FireShot_WhenEveryWriteConflicts_Is409GameUpdateConflict()
    {
        var game = await StoreAsync();
        // Each read is followed by another write, so every attempt carries a stale token.
        _factory.Games.AfterRead = () =>
            _factory.Games.TryUpdateAsync(game, _factory.Games.TokenOf(game.GameCode), CancellationToken.None);

        try
        {
            using var response = await FireAsync(game.GameCode, Token(game.PlayerOne.PlayerId), Body(3, 7));

            await AssertProblemAsync(response, HttpStatusCode.Conflict, "game-update-conflict");
        }
        finally
        {
            _factory.Games.AfterRead = null;
        }
    }

    [Fact]
    public async Task FireShot_LowerCaseCode_Is200WithTheResultAndPassesTheTurn()
    {
        var game = await StoreAsync();

        using var response = await FireAsync(game.GameCode.ToLowerInvariant(), Token(game.PlayerOne.PlayerId), Body(2, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, body.GetProperty("column").GetInt32());
        Assert.Equal(0, body.GetProperty("row").GetInt32());
        Assert.True(body.GetProperty("hit").GetBoolean());
        var stored = await ReadAsync(game);
        Assert.Equal([new Shot(2, 0, true)], stored.PlayerOne.Shots);
        Assert.True(stored.PlayerTwo!.HasTurn);
        _factory.Events.Verify(e => e.SendAsync(
            It.Is<GameShotFiredEvent>(s => s.Hit && s.Column == 2 && s.Row == 0), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()), Times.Once);
        _factory.Events.Verify(e => e.SendAsync(
            It.Is<GameTurnChangedEvent>(t => t.PlayerWithTurnId == game.PlayerTwo!.PlayerId), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FireShot_ShowsUpInTheOpponentsView()
    {
        var game = await StoreAsync();
        using var fired = await FireAsync(game.GameCode, Token(game.PlayerOne.PlayerId), Body(2, 0));

        using var response = await SendAsync(HttpMethod.Get, $"/games/{game.GameCode}", Token(game.PlayerTwo!.PlayerId));

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var shot = Assert.Single(body.GetProperty("opponent").GetProperty("shots").EnumerateArray());
        Assert.Equal(2, shot.GetProperty("column").GetInt32());
        Assert.True(shot.GetProperty("hit").GetBoolean());
        Assert.True(body.GetProperty("me").GetProperty("hasTurn").GetBoolean());
    }

    private static object Body(int column, int row) => new { column, row };

    private Task<HttpResponseMessage> FireAsync(string code, string? token, object body) =>
        SendAsync(HttpMethod.Post, $"/games/{code}/shots", token, body);

    private static async Task AssertValidationAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.All(fields, field => Assert.Contains(field, problem!.Errors.Keys));
    }

    private static string Token(Guid playerId) => TestTokens.Issue(playerId, GameFactory.PlayerName());

    // By default a started game in which player one has the turn and both fleets are placed.
    private async Task<Game> StoreAsync(Game? game = null)
    {
        game ??= GameFactory.Stored(
            GameState.Started,
            GameFactory.Player(hasTurn: true, isReady: true, ships: GameFactory.FullFleet()),
            GameFactory.Player(isReady: true, ships: GameFactory.FullFleet()));
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
