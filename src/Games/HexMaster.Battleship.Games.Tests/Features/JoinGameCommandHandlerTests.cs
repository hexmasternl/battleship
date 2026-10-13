using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.JoinGame;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.Games.Tests.Fakes;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Features;

public class JoinGameCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    private readonly InMemoryGameRepository _games = new();
    private readonly Mock<IIntegrationEventSender> _events = new();
    private readonly Mock<ILogger<JoinGameCommandHandler>> _logger = new();
    private readonly Func<long> _gamesJoined;
    private readonly JoinGameCommandHandler _handler;

    public JoinGameCommandHandlerTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var telemetry = TelemetryFactory.Create(out var meterFactory);
        _gamesJoined = TelemetryFactory.Listen(meterFactory, GamesMetrics.GamesJoinedName);
        var updater = new GameUpdater(_games, telemetry, NullLogger<GameUpdater>.Instance);
        _handler = new JoinGameCommandHandler(updater, _events.Object, telemetry, new FakeTimeProvider(Later), _logger.Object);
    }

    [Fact]
    public async Task Handle_SeatsTheCallerAndAnnouncesTheJoinToBothPlayers()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New, withoutPlayerTwo: true, lastActivityOn: Now));
        var playerId = Guid.NewGuid();

        var result = await _handler.Handle(new JoinGameCommand(game.Code, playerId, "Admiral Tromp"), CancellationToken.None);

        Assert.Equal(game.Id, result.Game!.GameId);
        Assert.Equal(game.GameCode, result.Game.GameCode);
        var stored = await _games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(playerId, stored!.PlayerTwo!.PlayerId);
        Assert.Equal("Admiral Tromp", stored.PlayerTwo.PlayerName);
        Assert.Equal(GameState.New, stored.GameState);
        Assert.Equal(Later, stored.LastActivityOn);
        Assert.Equal(1, _gamesJoined());
        _events.Verify(e => e.SendAsync(
            new GameJoinedEvent(game.Id, game.GameCode, playerId, "Admiral Tromp"),
            It.Is<EventAudience>(a => a.Target == EventTarget.Game
                && a.TargetIds.SequenceEqual(new[] { game.PlayerOne.PlayerId.ToString(), playerId.ToString() })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForAPlayerOfTheGameChangesNothingAndAnnouncesNothing()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.Started, lastActivityOn: Now));

        var result = await _handler.Handle(
            new JoinGameCommand(game.Code, game.PlayerTwo!.PlayerId, game.PlayerTwo.PlayerName), CancellationToken.None);

        Assert.Equal(game.Id, result.Game!.GameId);
        var stored = await _games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(Now, stored!.LastActivityOn);
        Assert.Equal(0, _gamesJoined());
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundForAnUnknownCode()
    {
        var result = await _handler.Handle(
            new JoinGameCommand(GameFactory.Code(), Guid.NewGuid(), GameFactory.PlayerName()), CancellationToken.None);

        Assert.Null(result.Game);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_LetsARefusalPropagateWithoutAnnouncing()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New, lastActivityOn: Now));

        var refused = await Assert.ThrowsAsync<GameJoinRefusedException>(() => _handler.Handle(
            new JoinGameCommand(game.Code, Guid.NewGuid(), GameFactory.PlayerName()), CancellationToken.None));

        Assert.Equal(GameJoinRefusedReason.GameFull, refused.Reason);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_KeepsTheJoinWhenPublishingFailsAndLogsTheCode()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New, withoutPlayerTwo: true, lastActivityOn: Now));
        var playerId = Guid.NewGuid();
        _events.Setup(e => e.SendAsync(It.IsAny<GameJoinedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The sidecar is down."));

        var result = await _handler.Handle(new JoinGameCommand(game.Code, playerId, "Admiral Tromp"), CancellationToken.None);

        Assert.NotNull(result.Game);
        var stored = await _games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(playerId, stored!.PlayerTwo!.PlayerId);
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(game.GameCode, StringComparison.Ordinal)),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private async Task<Game> StoreAsync(Game game)
    {
        await _games.TryAddAsync(game, CancellationToken.None);
        return game;
    }
}
