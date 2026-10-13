using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.MarkReady;
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

public class MarkReadyCommandHandlerTests
{
    private static readonly DateTimeOffset Later = new(2026, 10, 13, 10, 5, 0, TimeSpan.Zero);

    private readonly InMemoryGameRepository _games = new();
    private readonly Mock<IIntegrationEventSender> _events = new();
    private readonly Mock<ILogger<MarkReadyCommandHandler>> _logger = new();
    private readonly List<(object Event, EventAudience Audience)> _sent = [];
    private readonly Func<long> _playersReady;
    private readonly Func<long> _gamesStarted;
    private readonly MarkReadyCommandHandler _handler;

    public MarkReadyCommandHandlerTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _events.Setup(e => e.SendAsync(It.IsAny<GamePlayerReadyEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .Callback<GamePlayerReadyEvent, EventAudience, CancellationToken>((e, a, _) => _sent.Add((e, a)))
            .Returns(Task.CompletedTask);
        _events.Setup(e => e.SendAsync(It.IsAny<GameStartedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .Callback<GameStartedEvent, EventAudience, CancellationToken>((e, a, _) => _sent.Add((e, a)))
            .Returns(Task.CompletedTask);
        var telemetry = TelemetryFactory.Create(out var meterFactory);
        _playersReady = TelemetryFactory.Listen(meterFactory, GamesMetrics.PlayersReadyName);
        _gamesStarted = TelemetryFactory.Listen(meterFactory, GamesMetrics.GamesStartedName);
        _handler = new MarkReadyCommandHandler(
            new GameUpdater(_games, telemetry, NullLogger<GameUpdater>.Instance),
            new PlayerTwoFirst(),
            _events.Object,
            telemetry,
            new FakeTimeProvider(Later),
            _logger.Object);
    }

    [Fact]
    public async Task Handle_FirstReadyAnnouncesItToBothPlayersWithoutStarting()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New, playerOne: GameFactory.Player(ships: GameFactory.FullFleet())));

        var view = await _handler.Handle(new MarkReadyCommand(game.Code, game.PlayerOne.PlayerId), CancellationToken.None);

        Assert.True(view!.Me.Ready);
        Assert.Equal(GameState.New, view.State);
        var (sent, audience) = Assert.Single(_sent);
        Assert.Equal(new GamePlayerReadyEvent(game.Id, game.GameCode, game.PlayerOne.PlayerId), sent);
        AssertAudience(audience, game.PlayerOne.PlayerId, game.PlayerTwo!.PlayerId);
        Assert.Equal(1, _playersReady());
        Assert.Equal(0, _gamesStarted());
    }

    [Fact]
    public async Task Handle_ReadyWhileAloneTargetsOnlyTheCaller()
    {
        var game = await StoreAsync(GameFactory.Stored(
            GameState.New, playerOne: GameFactory.Player(ships: GameFactory.FullFleet()), withoutPlayerTwo: true));

        await _handler.Handle(new MarkReadyCommand(game.Code, game.PlayerOne.PlayerId), CancellationToken.None);

        AssertAudience(Assert.Single(_sent).Audience, game.PlayerOne.PlayerId);
    }

    [Fact]
    public async Task Handle_SecondReadyStartsTheGameAndAnnouncesReadyThenStarted()
    {
        var game = await StoreAsync(GameFactory.Stored(
            GameState.New,
            playerOne: GameFactory.Player(isReady: true, ships: GameFactory.FullFleet()),
            playerTwo: GameFactory.Player(ships: GameFactory.FullFleet())));
        var playerTwo = game.PlayerTwo!.PlayerId;

        var view = await _handler.Handle(new MarkReadyCommand(game.Code, playerTwo), CancellationToken.None);

        Assert.Equal(GameState.Started, view!.State);
        Assert.True(view.Me.HasTurn);
        Assert.Equal(
            [(object)new GamePlayerReadyEvent(game.Id, game.GameCode, playerTwo), new GameStartedEvent(game.Id, game.GameCode, playerTwo)],
            _sent.Select(s => s.Event));
        Assert.All(_sent, s => AssertAudience(s.Audience, game.PlayerOne.PlayerId, playerTwo));
        Assert.Equal(1, _gamesStarted());
    }

    [Fact]
    public async Task Handle_AlreadyReadyWritesAndSendsNothing()
    {
        var game = await StoreAsync(GameFactory.Stored(
            GameState.New, playerOne: GameFactory.Player(isReady: true, ships: GameFactory.FullFleet())));
        var token = _games.TokenOf(game.GameCode);

        var view = await _handler.Handle(new MarkReadyCommand(game.Code, game.PlayerOne.PlayerId), CancellationToken.None);

        Assert.True(view!.Me.Ready);
        Assert.Equal(token, _games.TokenOf(game.GameCode));
        Assert.Empty(_sent);
        Assert.Equal(0, _playersReady());
    }

    [Fact]
    public async Task Handle_ReturnsNullForAnUnknownGameOrSomeoneWhoDoesNotPlay()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New));

        Assert.Null(await _handler.Handle(new MarkReadyCommand(GameFactory.Code(), Guid.NewGuid()), CancellationToken.None));
        Assert.Null(await _handler.Handle(new MarkReadyCommand(game.Code, Guid.NewGuid()), CancellationToken.None));
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Handle_LetsAnIncompleteFleetThroughWithoutAWrite()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New));
        var token = _games.TokenOf(game.GameCode);

        var refused = await Assert.ThrowsAsync<PlacementRefusedException>(
            () => _handler.Handle(new MarkReadyCommand(game.Code, game.PlayerOne.PlayerId), CancellationToken.None));

        Assert.Equal(PlacementRefusedReason.FleetIncomplete, refused.Reason);
        Assert.Equal(token, _games.TokenOf(game.GameCode));
    }

    [Fact]
    public async Task Handle_SwallowsAPublishFailureAndKeepsTheReadyState()
    {
        _events.Setup(e => e.SendAsync(It.IsAny<GamePlayerReadyEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The sidecar is down."));
        var game = await StoreAsync(GameFactory.Stored(GameState.New, playerOne: GameFactory.Player(ships: GameFactory.FullFleet())));

        var view = await _handler.Handle(new MarkReadyCommand(game.Code, game.PlayerOne.PlayerId), CancellationToken.None);

        Assert.True(view!.Me.Ready);
        Assert.True((await _games.GetByCodeAsync(game.GameCode, CancellationToken.None))!.PlayerOne.IsReady);
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(game.GameCode, StringComparison.Ordinal)),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

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

    private sealed class PlayerTwoFirst : IFirstTurnPicker
    {
        public Guid Pick(Guid playerOneId, Guid playerTwoId) => playerTwoId;
    }
}
