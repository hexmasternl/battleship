using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Sessions;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Sessions;

public sealed class ReconcileTimerServiceTests
{
    [Fact]
    public async Task Tick_TurnEventWasLost_StillFires()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var code = GameViewFactory.Code();
        var games = new Mock<IGamesClient>();
        games.Setup(g => g.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(
                GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: true)));
        games.Setup(g => g.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<FireShotResponse>.Ok(new FireShotResponse(0, 0, false, GameState.Started)));
        var settings = new BotSettings(new DevBotOptions { ShotDelay = TimeSpan.Zero });
        var sessions = SessionsFactory.Create(games.Object, settings, clock);
        sessions.Track(code);
        var service = new ReconcileTimerService(sessions, settings, clock, NullLogger<ReconcileTimerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // ExecuteAsync starts on the thread pool and creates the timer there.
        clock.Advance(TimeSpan.FromSeconds(14));
        await Task.Delay(50);
        games.Verify(g => g.FireAsync(It.IsAny<string>(), It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Never);

        clock.Advance(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 200 && !games.Invocations.Any(inv => inv.Method.Name == nameof(IGamesClient.FireAsync)); i++)
        {
            await Task.Delay(10);
        }

        await service.StopAsync(CancellationToken.None);

        games.Verify(g => g.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tick_ReconcileThrows_KeepsTicking()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var code = GameViewFactory.Code();
        var games = new Mock<IGamesClient>();
        games.Setup(g => g.GetGameAsync(code, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
        var settings = new BotSettings(new DevBotOptions());
        var sessions = SessionsFactory.Create(games.Object, settings, clock);
        sessions.Track(code);
        var service = new ReconcileTimerService(sessions, settings, clock, NullLogger<ReconcileTimerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        for (var tick = 0; tick < 2; tick++)
        {
            var before = games.Invocations.Count;
            clock.Advance(settings.ReconcileInterval);
            for (var i = 0; i < 200 && games.Invocations.Count == before; i++)
            {
                await Task.Delay(10);
            }
        }

        await service.StopAsync(CancellationToken.None);

        games.Verify(g => g.GetGameAsync(code, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
