using System.Net;
using System.Text.Json;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Client;

public sealed class GamesClientTests
{
    private readonly string code = GameViewFactory.Code();

    [Fact]
    public async Task JoinAsync_Ok_PostsToJoinAndReturnsTheGame()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.JsonResponse(HttpStatusCode.OK, new JoinGameResponse(Guid.NewGuid(), code)));
        var client = new GamesClient(handler.CreateClient());

        var result = await client.JoinAsync(code, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Equal(code, result.Value.GameCode);
        var (request, _) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"http://gateway/games/{code}/join", request.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "game-not-found", GameCallStatus.NotFound)]
    [InlineData(HttpStatusCode.Conflict, "game-full", GameCallStatus.GameFull)]
    [InlineData(HttpStatusCode.Conflict, "game-already-started", GameCallStatus.GameAlreadyStarted)]
    [InlineData(HttpStatusCode.Conflict, "game-update-conflict", GameCallStatus.UpdateConflict)]
    [InlineData(HttpStatusCode.Unauthorized, null, GameCallStatus.Unauthorized)]
    public async Task JoinAsync_Problem_MapsToStatus(HttpStatusCode status, string? slug, GameCallStatus expected)
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Problem(status, slug, "The game already has two players."));
        var client = new GamesClient(handler.CreateClient());

        var result = await client.JoinAsync(code, CancellationToken.None);

        Assert.False(result.IsOk);
        Assert.Equal(expected, result.Status);
        Assert.Equal("The game already has two players.", result.Detail);
    }

    [Fact]
    public async Task GetGameAsync_Ok_ReadsTheViewWithStateByName()
    {
        var view = GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: true,
            shots: [ShotFactory.Hit(3, 4)]);
        var handler = new StubHttpHandler(_ => StubHttpHandler.JsonResponse(HttpStatusCode.OK, view));
        var client = new GamesClient(handler.CreateClient());

        var result = await client.GetGameAsync(code, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Equal(GameState.Started, result.Value.State);
        Assert.True(result.Value.Me.HasTurn);
        Assert.Equal(5, result.Value.Me.Ships.Count);
        Assert.Equal(ShotFactory.Hit(3, 4), Assert.Single(result.Value.Me.Shots));
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Request.Method);
    }

    [Fact]
    public async Task GetGameAsync_NotFound_MapsToNotFound()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Problem(HttpStatusCode.NotFound, "game-not-found"));

        var result = await new GamesClient(handler.CreateClient()).GetGameAsync(code, CancellationToken.None);

        Assert.Equal(GameCallStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task PlaceShipAsync_PutsTheKindAndPlacementInTheWireFormat()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.JsonResponse(HttpStatusCode.OK, GameViewFactory.View(code)));
        var client = new GamesClient(handler.CreateClient());

        var result = await client.PlaceShipAsync(code,
            new ShipPlacement(ShipKind.Carrier, new Coordinate(2, 7), Orientation.Vertical), CancellationToken.None);

        Assert.True(result.IsOk);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"http://gateway/games/{code}/ships/carrier", request.RequestUri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal(2, json.RootElement.GetProperty("column").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("row").GetInt32());
        Assert.Equal("vertical", json.RootElement.GetProperty("orientation").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "placement-overlap", GameCallStatus.PlacementRefused)]
    [InlineData(HttpStatusCode.BadRequest, "placement-out-of-board", GameCallStatus.PlacementRefused)]
    [InlineData(HttpStatusCode.Conflict, "player-ready", GameCallStatus.PlayerReady)]
    [InlineData(HttpStatusCode.Conflict, "game-already-started", GameCallStatus.GameAlreadyStarted)]
    [InlineData(HttpStatusCode.BadRequest, null, GameCallStatus.Failed)]
    public async Task PlaceShipAsync_Problem_MapsToStatus(HttpStatusCode status, string? slug, GameCallStatus expected)
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Problem(status, slug));

        var result = await new GamesClient(handler.CreateClient()).PlaceShipAsync(code,
            new ShipPlacement(ShipKind.Destroyer, new Coordinate(0, 0), Orientation.Horizontal), CancellationToken.None);

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "fleet-incomplete", GameCallStatus.FleetIncomplete)]
    [InlineData(HttpStatusCode.NotFound, "game-not-found", GameCallStatus.NotFound)]
    public async Task ReadyAsync_Problem_MapsToStatus(HttpStatusCode status, string slug, GameCallStatus expected)
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Problem(status, slug));

        var result = await new GamesClient(handler.CreateClient()).ReadyAsync(code, CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Equal($"http://gateway/games/{code}/ready", handler.Requests[0].Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task ReadyAsync_Ok_ReturnsTheView()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.JsonResponse(HttpStatusCode.OK, GameViewFactory.View(code, ready: true)));

        var result = await new GamesClient(handler.CreateClient()).ReadyAsync(code, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.True(result.Value.Me.Ready);
    }

    [Fact]
    public async Task FireAsync_Ok_PostsTheCellAndReadsTheOutcome()
    {
        var handler = new StubHttpHandler(_ =>
            StubHttpHandler.JsonResponse(HttpStatusCode.OK, new FireShotResponse(4, 9, true, GameState.Completed)));

        var result = await new GamesClient(handler.CreateClient()).FireAsync(code, new Coordinate(4, 9), CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.True(result.Value.Hit);
        Assert.Equal(GameState.Completed, result.Value.State);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal($"http://gateway/games/{code}/shots", request.RequestUri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal(4, json.RootElement.GetProperty("column").GetInt32());
        Assert.Equal(9, json.RootElement.GetProperty("row").GetInt32());
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "not-your-turn", GameCallStatus.NotYourTurn)]
    [InlineData(HttpStatusCode.Conflict, "cell-already-shot", GameCallStatus.CellAlreadyShot)]
    [InlineData(HttpStatusCode.Conflict, "game-not-started", GameCallStatus.GameNotStarted)]
    [InlineData(HttpStatusCode.NotFound, "game-not-found", GameCallStatus.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, null, GameCallStatus.Failed)]
    public async Task FireAsync_Problem_MapsToStatus(HttpStatusCode status, string? slug, GameCallStatus expected)
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Problem(status, slug));

        var result = await new GamesClient(handler.CreateClient()).FireAsync(code, new Coordinate(1, 1), CancellationToken.None);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Problem_WithoutAJsonBody_StillMapsTheStatus()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("nope") });

        var result = await new GamesClient(handler.CreateClient()).GetGameAsync(code, CancellationToken.None);

        Assert.Equal(GameCallStatus.NotFound, result.Status);
    }
}
