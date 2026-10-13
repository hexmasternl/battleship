using System.Text.Json;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Events;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Sessions;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.IntegrationEvents.Games;
using HexMaster.Battleship.IntegrationEvents.Users;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Events;

public sealed class IntegrationEventHandlerTests
{
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IGamesClient> games = new();
    private readonly Guid botId = Guid.NewGuid();
    private readonly Guid humanId = Guid.NewGuid();
    private readonly string code = GameViewFactory.Code();
    private readonly FakeLogCollector logs = new();
    private readonly BotSettings settings = new(new DevBotOptions { ShotDelay = TimeSpan.Zero });
    private readonly GameSessions sessions;
    private readonly IntegrationEventHandler handler;

    public IntegrationEventHandlerTests()
    {
        var users = new Mock<IUsersClient>();
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TokenFactory.Pair(botId, clock.GetUtcNow()));
        var tokens = new TokenStore(users.Object, clock, NullLogger<TokenStore>.Instance);

        var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(logs)));
        sessions = new GameSessions(games.Object, settings, clock, loggerFactory, Mock.Of<IHostApplicationLifetime>())
        {
            RandomFor = _ => new Random(5),
        };
        handler = new IntegrationEventHandler(sessions, settings, tokens, clock, NullLogger<IntegrationEventHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_GameCreatedByAnotherPlayer_JoinsAfterTheGracePeriod()
    {
        games.Setup(g => g.JoinAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<JoinGameResponse>.Ok(new JoinGameResponse(Guid.NewGuid(), code)));
        games.Setup(g => g.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code, ships: GameViewFactory.FullFleet(), ready: true)));

        var disposition = await handler.HandleAsync(Created(clock.GetUtcNow()), CancellationToken.None);

        Assert.Equal(EventDisposition.JoinScheduled, disposition);
        clock.Advance(TimeSpan.FromSeconds(9));
        await Task.Delay(50);
        games.Verify(g => g.JoinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await AdvanceUntilDone(TimeSpan.FromSeconds(1));

        games.Verify(g => g.JoinAsync(code, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal([code], sessions.ActiveCodes);
    }

    [Fact]
    public async Task HandleAsync_HumanJoinsFirst_DropsTheGameSilently()
    {
        games.Setup(g => g.JoinAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<JoinGameResponse>.Problem(GameCallStatus.GameFull, "The game already has two players."));

        await handler.HandleAsync(Created(clock.GetUtcNow()), CancellationToken.None);
        await AdvanceUntilDone(settings.JoinGracePeriod);

        Assert.Empty(sessions.ActiveCodes);
        Assert.DoesNotContain(logs.GetSnapshot(), r => r.Level >= LogLevel.Warning);
        Assert.Contains(logs.GetSnapshot(), r => r.Level == LogLevel.Debug && r.Message.Contains(code));
    }

    [Fact]
    public async Task HandleAsync_AutoJoinSwitchedOffDuringTheGracePeriod_DoesNotJoin()
    {
        await handler.HandleAsync(Created(clock.GetUtcNow()), CancellationToken.None);
        settings.AutoJoin = false;
        await AdvanceUntilDone(settings.JoinGracePeriod);

        games.Verify(g => g.JoinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AutoJoinOff_IgnoresNewGames()
    {
        settings.AutoJoin = false;

        var disposition = await handler.HandleAsync(Created(clock.GetUtcNow()), CancellationToken.None);

        Assert.Equal(EventDisposition.AutoJoinOff, disposition);
        Assert.Null(sessions.LastBackgroundTask);
    }

    [Fact]
    public async Task HandleAsync_BotsOwnGame_IsNotJoined()
    {
        var envelope = EnvelopeFactory.Create(new GameCreatedEvent(Guid.NewGuid(), code, botId), clock.GetUtcNow(), botId);

        Assert.Equal(EventDisposition.OwnGame, await handler.HandleAsync(envelope, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_StaleEnvelope_IsIgnored()
    {
        var occurred = clock.GetUtcNow() - settings.JoinGracePeriod - TimeSpan.FromSeconds(61);

        Assert.Equal(EventDisposition.Stale, await handler.HandleAsync(Created(occurred), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_TurnChangedForATrackedGame_TriggersAReconcile()
    {
        sessions.Track(code);
        games.Setup(g => g.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true)));
        var envelope = EnvelopeFactory.Create(new GameTurnChangedEvent(Guid.NewGuid(), code, botId), clock.GetUtcNow(), humanId, botId);

        var disposition = await handler.HandleAsync(envelope, CancellationToken.None);
        await sessions.LastBackgroundTask!;

        Assert.Equal(EventDisposition.ReconcileTriggered, disposition);
        games.Verify(g => g.GetGameAsync(code, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GameEventNotForTheBot_IsIgnored()
    {
        sessions.Track(code);
        var envelope = EnvelopeFactory.Create(new GameStartedEvent(Guid.NewGuid(), code, humanId), clock.GetUtcNow(), humanId);

        Assert.Equal(EventDisposition.Ignored, await handler.HandleAsync(envelope, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_GameEventForAnUntrackedGame_IsIgnored()
    {
        var envelope = EnvelopeFactory.Create(new GameEndedEvent(Guid.NewGuid(), code, GameEndReason.Completed, humanId), clock.GetUtcNow(), humanId, botId);

        Assert.Equal(EventDisposition.Ignored, await handler.HandleAsync(envelope, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_UnrelatedEventType_IsIgnored()
    {
        var envelope = EnvelopeFactory.Create(new UserCreatedEvent(Guid.NewGuid(), "Someone"), clock.GetUtcNow(), humanId);

        Assert.Equal(EventDisposition.Ignored, await handler.HandleAsync(envelope, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_PayloadWithoutGameCode_IsIgnored()
    {
        var envelope = new IntegrationEventEnvelope(Guid.NewGuid().ToString(), GameCreatedEvent.EventType, EventTarget.User,
            [humanId.ToString()], clock.GetUtcNow(), JsonSerializer.SerializeToElement(new { gameId = Guid.NewGuid() }));

        Assert.Equal(EventDisposition.Ignored, await handler.HandleAsync(envelope, CancellationToken.None));
    }

    private IntegrationEventEnvelope Created(DateTimeOffset occurredAt) =>
        EnvelopeFactory.Create(new GameCreatedEvent(Guid.NewGuid(), code, humanId), occurredAt, humanId);

    // The scheduled join waits on the fake clock in the background; move the clock until it is done.
    private async Task AdvanceUntilDone(TimeSpan step)
    {
        var task = sessions.LastBackgroundTask!;
        for (var i = 0; i < 200 && !task.IsCompleted; i++)
        {
            clock.Advance(step);
            await Task.Delay(10);
        }

        await task;
    }
}
