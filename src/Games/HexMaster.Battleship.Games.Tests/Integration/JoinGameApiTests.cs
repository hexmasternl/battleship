using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Integration;

public class JoinGameApiTests : IClassFixture<JoinGameApiFactory>
{
    private readonly JoinGameApiFactory _factory;

    public JoinGameApiTests(JoinGameApiFactory factory)
    {
        _factory = factory;
        _factory.Events.Reset();
    }

    [Fact]
    public async Task Join_WithoutAToken_Is401AndChangesNothing()
    {
        var game = await StoreAsync(GameState.New, withoutPlayerTwo: true);

        using var response = await JoinAsync(game.GameCode, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null((await ReadAsync(game)).PlayerTwo);
    }

    [Fact]
    public async Task Join_WithATokenWithoutAName_Is401()
    {
        var game = await StoreAsync(GameState.New, withoutPlayerTwo: true);

        using var response = await JoinAsync(game.GameCode, TestTokens.Issue(Guid.NewGuid(), playerName: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null((await ReadAsync(game)).PlayerTwo);
    }

    [Fact]
    public async Task Join_UnknownCode_Is404GameNotFound()
    {
        using var response = await JoinAsync(GameFactory.Code().Value, Token());

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "https://battleship.hexmaster.nl/problems/game-not-found");
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ABCD-123")]
    [InlineData("ABCDEFGHI")]
    public async Task Join_MalformedCode_Is404GameNotFound(string code)
    {
        using var response = await JoinAsync(code, Token());

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "https://battleship.hexmaster.nl/problems/game-not-found");
    }

    [Fact]
    public async Task Join_FreeGame_Is200AndSeatsTheCallerFromTheToken()
    {
        var game = await StoreAsync(GameState.New, withoutPlayerTwo: true);
        var playerId = Guid.NewGuid();

        using var response = await JoinAsync(game.GameCode, TestTokens.Issue(playerId, "Admiral Tromp"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JoinGameResponse>();
        Assert.Equal(new JoinGameResponse(game.Id, game.GameCode), body);
        var stored = await ReadAsync(game);
        Assert.Equal(playerId, stored.PlayerTwo!.PlayerId);
        Assert.Equal("Admiral Tromp", stored.PlayerTwo.PlayerName);
        Assert.Equal(GameState.New, stored.GameState);
        Assert.False(stored.PlayerOne.HasTurn);
        Assert.False(stored.PlayerTwo.HasTurn);
        _factory.Events.Verify(e => e.SendAsync(
            new GameJoinedEvent(game.Id, game.GameCode, playerId, "Admiral Tromp"),
            It.IsAny<EventAudience>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Join_AcceptsALowerCaseCode()
    {
        var game = await StoreAsync(GameState.New, withoutPlayerTwo: true);
        var playerId = Guid.NewGuid();

        using var response = await JoinAsync(game.GameCode.ToLowerInvariant(), TestTokens.Issue(playerId, "Admiral Tromp"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(game.GameCode, (await response.Content.ReadFromJsonAsync<JoinGameResponse>())!.GameCode);
        Assert.Equal(playerId, (await ReadAsync(game)).PlayerTwo!.PlayerId);
    }

    [Fact]
    public async Task Join_Again_Is200WithoutAChangeOrAnEvent()
    {
        var game = await StoreAsync(GameState.New);
        var playerTwo = game.PlayerTwo!;

        using var response = await JoinAsync(game.GameCode, TestTokens.Issue(playerTwo.PlayerId, playerTwo.PlayerName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(game.LastActivityOn, (await ReadAsync(game)).LastActivityOn);
        _factory.Events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Join_OwnGame_Is200AndLeavesTheSecondSeatEmpty()
    {
        var game = await StoreAsync(GameState.New, withoutPlayerTwo: true);

        using var response = await JoinAsync(game.GameCode, TestTokens.Issue(game.PlayerOne.PlayerId, game.PlayerOne.PlayerName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await ReadAsync(game)).PlayerTwo);
        _factory.Events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Join_FullGame_Is409GameFull()
    {
        var game = await StoreAsync(GameState.New);

        using var response = await JoinAsync(game.GameCode, Token());

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "https://battleship.hexmaster.nl/problems/game-full");
        Assert.Equal(game.PlayerTwo!.PlayerId, (await ReadAsync(game)).PlayerTwo!.PlayerId);
    }

    [Fact]
    public async Task Join_StartedGame_Is409GameAlreadyStarted()
    {
        var game = await StoreAsync(GameState.Started);

        using var response = await JoinAsync(game.GameCode, Token());

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "https://battleship.hexmaster.nl/problems/game-already-started");
    }

    [Fact]
    public async Task Join_ThatKeepsConflicting_Is409GameUpdateConflict()
    {
        var game = await StoreAsync(GameState.New, withoutPlayerTwo: true);
        // Another request stores a change after every read, so each attempt holds a stale token.
        _factory.Games.AfterRead = () => _factory.Games.TryUpdateAsync(
            game, _factory.Games.TokenOf(game.GameCode), CancellationToken.None);

        try
        {
            using var response = await JoinAsync(game.GameCode, Token());

            await AssertProblemAsync(response, HttpStatusCode.Conflict, "https://battleship.hexmaster.nl/problems/game-update-conflict");
        }
        finally
        {
            _factory.Games.AfterRead = null;
        }
    }

    private static string Token() => TestTokens.Issue(Guid.NewGuid(), GameFactory.PlayerName());

    private async Task<Game> StoreAsync(GameState state, bool withoutPlayerTwo = false)
    {
        var game = GameFactory.Stored(state, withoutPlayerTwo: withoutPlayerTwo);
        await _factory.Games.TryAddAsync(game, CancellationToken.None);
        return game;
    }

    private async Task<IGame> ReadAsync(Game game) =>
        (await _factory.Games.GetByCodeAsync(game.GameCode, CancellationToken.None))!;

    private async Task<HttpResponseMessage> JoinAsync(string code, string? token)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/games/{code}/join");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(type, problem!.Type);
    }
}
