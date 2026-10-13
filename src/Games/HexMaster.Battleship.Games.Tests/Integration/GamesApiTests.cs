using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Integration;

public class GamesApiTests : IClassFixture<GamesApiFactory>
{
    private readonly GamesApiFactory _factory;
    private readonly List<IGame> _stored = [];

    public GamesApiTests(GamesApiFactory factory)
    {
        _factory = factory;
        _factory.Games.Reset();
        _factory.Events.Reset();
        _factory.Games.Setup(g => g.TryAddAsync(It.IsAny<IGame>(), It.IsAny<CancellationToken>()))
            .Callback<IGame, CancellationToken>((game, _) => _stored.Add(game))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task CreateGame_WithoutAToken_Is401AndStoresNothing()
    {
        using var response = await PostAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task CreateGame_WithAnExpiredToken_Is401AndStoresNothing()
    {
        var token = TestTokens.Issue(Guid.NewGuid(), "Admiral Nelson", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        using var response = await PostAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task CreateGame_WithATokenSignedByAnotherKey_Is401()
    {
        var token = TestTokens.Issue(Guid.NewGuid(), "Admiral Nelson", signingKey: new string('x', 32));

        using var response = await PostAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task CreateGame_WithATokenWithoutAName_Is401()
    {
        using var response = await PostAsync(TestTokens.Issue(Guid.NewGuid(), playerName: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task CreateGame_Is201WithTheGameAndItsLocation()
    {
        var playerId = Guid.NewGuid();

        using var response = await PostAsync(TestTokens.Issue(playerId, "Admiral Nelson"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateGameResponse>();
        var game = Assert.Single(_stored);
        Assert.Equal(game.Id, body!.GameId);
        Assert.Equal(game.GameCode, body.GameCode);
        Assert.Matches("^[A-Z0-9]{8}$", body.GameCode);
        Assert.Equal($"/games/{body.GameCode}", response.Headers.Location!.OriginalString);
        Assert.Equal(GameState.New, game.GameState);
        Assert.Null(game.PlayerTwo);
        _factory.Events.Verify(e => e.SendAsync(
            new GameCreatedEvent(game.Id, game.GameCode, playerId),
            It.Is<EventAudience>(a => a.TargetIds.SequenceEqual(new[] { playerId.ToString() })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateGame_TakesTheCreatorFromTheTokenAndIgnoresTheBody()
    {
        var playerId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new { playerId = Guid.NewGuid(), playerName = "Impostor" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Issue(playerId, "Admiral Nelson"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var game = Assert.Single(_stored);
        Assert.Equal(playerId, game.PlayerOne.PlayerId);
        Assert.Equal("Admiral Nelson", game.PlayerOne.PlayerName);
    }

    [Fact]
    public async Task CreateGame_StillSucceedsWhenPublishingFails()
    {
        _factory.Events.Setup(e => e.SendAsync(It.IsAny<GameCreatedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The sidecar is down."));

        using var response = await PostAsync(TestTokens.Issue(Guid.NewGuid(), GameFactory.PlayerName()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(_stored);
    }

    private async Task<HttpResponseMessage> PostAsync(string? token)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/games");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }
}
