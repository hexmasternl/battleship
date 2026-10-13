using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.FireShot;
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

public class FireShotCommandHandlerTests
{
    private static readonly DateTimeOffset Later = new(2026, 10, 13, 10, 5, 0, TimeSpan.Zero);

    // The full fleet lies on the even rows from column 0: (2, 0) is a carrier cell, (11, 11) water.
    private static readonly Coordinate CarrierCell = new(2, 0);
    private static readonly Coordinate Water = new(11, 11);

    private readonly InMemoryGameRepository _games = new();
    private readonly Mock<IIntegrationEventSender> _events = new();
    private readonly Mock<ILogger<FireShotCommandHandler>> _logger = new();
    private readonly List<(object Event, EventAudience Audience)> _sent = [];
    private readonly Func<long> _shotsFired;
    private readonly FireShotCommandHandler _handler;

    public FireShotCommandHandlerTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _events.Setup(e => e.SendAsync(It.IsAny<GameShotFiredEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .Callback<GameShotFiredEvent, EventAudience, CancellationToken>((e, a, _) => _sent.Add((e, a)))
            .Returns(Task.CompletedTask);
        _events.Setup(e => e.SendAsync(It.IsAny<GameTurnChangedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .Callback<GameTurnChangedEvent, EventAudience, CancellationToken>((e, a, _) => _sent.Add((e, a)))
            .Returns(Task.CompletedTask);
        var telemetry = TelemetryFactory.Create(out var meterFactory);
        _shotsFired = TelemetryFactory.Listen(meterFactory, GamesMetrics.ShotsFiredName);
        _handler = new FireShotCommandHandler(
            new GameUpdater(_games, telemetry, NullLogger<GameUpdater>.Instance),
            _events.Object,
            telemetry,
            new FakeTimeProvider(Later),
            _logger.Object);
    }

    [Theory]
    [InlineData(2, 0, true)]
    [InlineData(11, 11, false)]
    public async Task Handle_RecordsTheShotAndAnnouncesShotThenTurnToBothPlayers(int column, int row, bool hit)
    {
        var game = await StoreAsync(StartedGame());
        var shooter = game.PlayerOne.PlayerId;
        var opponent = game.PlayerTwo!.PlayerId;

        var result = await _handler.Handle(new FireShotCommand(game.Code, shooter, new Coordinate(column, row)), CancellationToken.None);

        Assert.Equal(new FireShotResponse(column, row, hit), result.Shot);
        Assert.Equal(
            [
                (object)new GameShotFiredEvent(game.Id, game.GameCode, shooter, column, row, hit),
                new GameTurnChangedEvent(game.Id, game.GameCode, opponent),
            ],
            _sent.Select(s => s.Event));
        Assert.All(_sent, s => AssertAudience(s.Audience, shooter, opponent));
        var stored = await _games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal([new Shot(column, row, hit)], stored!.PlayerOne.Shots);
        Assert.True(stored.PlayerTwo!.HasTurn);
        Assert.Equal(Later, stored.LastActivityOn);
        Assert.Equal(1, _shotsFired());
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundForAnUnknownGameOrSomeoneWhoDoesNotPlay()
    {
        var game = await StoreAsync(StartedGame());
        var token = _games.TokenOf(game.GameCode);

        Assert.Same(FireShotResult.NotFound, await _handler.Handle(new FireShotCommand(GameFactory.Code(), Guid.NewGuid(), Water), CancellationToken.None));
        Assert.Same(FireShotResult.NotFound, await _handler.Handle(new FireShotCommand(game.Code, Guid.NewGuid(), Water), CancellationToken.None));
        Assert.Equal(token, _games.TokenOf(game.GameCode));
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Handle_LetsARefusalThroughWithoutAWriteOrEvents()
    {
        var game = await StoreAsync(StartedGame());
        var token = _games.TokenOf(game.GameCode);

        var refused = await Assert.ThrowsAsync<ShotRefusedException>(
            () => _handler.Handle(new FireShotCommand(game.Code, game.PlayerTwo!.PlayerId, Water), CancellationToken.None));

        Assert.Equal(ShotRefusedReason.NotYourTurn, refused.Reason);
        Assert.Equal(token, _games.TokenOf(game.GameCode));
        Assert.Empty(_sent);
        Assert.Equal(0, _shotsFired());
    }

    [Fact]
    public async Task Handle_SwallowsAShotPublishFailureAndStillAnnouncesTheTurn()
    {
        _events.Setup(e => e.SendAsync(It.IsAny<GameShotFiredEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The sidecar is down."));
        var game = await StoreAsync(StartedGame());

        var result = await _handler.Handle(new FireShotCommand(game.Code, game.PlayerOne.PlayerId, CarrierCell), CancellationToken.None);

        Assert.True(result.Shot!.Hit);
        Assert.IsType<GameTurnChangedEvent>(Assert.Single(_sent).Event);
        Assert.Single((await _games.GetByCodeAsync(game.GameCode, CancellationToken.None))!.PlayerOne.Shots);
        VerifyErrorLogged(game.GameCode, Times.Once());
    }

    [Fact]
    public async Task Handle_SwallowsATurnPublishFailureAndKeepsTheShot()
    {
        _events.Setup(e => e.SendAsync(It.IsAny<GameTurnChangedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The sidecar is down."));
        var game = await StoreAsync(StartedGame());

        var result = await _handler.Handle(new FireShotCommand(game.Code, game.PlayerOne.PlayerId, Water), CancellationToken.None);

        Assert.False(result.Shot!.Hit);
        Assert.IsType<GameShotFiredEvent>(Assert.Single(_sent).Event);
        Assert.True((await _games.GetByCodeAsync(game.GameCode, CancellationToken.None))!.PlayerTwo!.HasTurn);
        VerifyErrorLogged(game.GameCode, Times.Once());
    }

    private void VerifyErrorLogged(string gameCode, Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(gameCode, StringComparison.Ordinal)),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    // A started game in which player one has the turn and both fleets are placed.
    internal static Game StartedGame() =>
        GameFactory.Stored(
            GameState.Started,
            GameFactory.Player(hasTurn: true, isReady: true, ships: GameFactory.FullFleet()),
            GameFactory.Player(isReady: true, ships: GameFactory.FullFleet()));

    private static void AssertAudience(EventAudience audience, params Guid[] playerIds)
    {
        Assert.Equal(EventTarget.Game, audience.Target);
        Assert.Equal(playerIds.Select(p => p.ToString()), audience.TargetIds);
    }

    private async Task<Game> StoreAsync(Game game)
    {
        await _games.TryAddAsync(game, CancellationToken.None);
        return game;
    }
}
