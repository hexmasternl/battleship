using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Sessions;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Sessions;

public sealed class GameSessionsTests
{
    private readonly string code = GameViewFactory.Code();
    private readonly Mock<IGamesClient> client = new();
    private readonly GameSessions sessions;

    public GameSessionsTests()
    {
        sessions = SessionsFactory.Create(client.Object, new BotSettings(new DevBotOptions { ShotDelay = TimeSpan.Zero }), new FakeTimeProvider());
    }

    [Fact]
    public async Task JoinAsync_Ok_TracksTheGameByItsUpperCaseCode()
    {
        client.Setup(c => c.JoinAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<JoinGameResponse>.Ok(new JoinGameResponse(Guid.NewGuid(), code)));

        var result = await sessions.JoinAsync(code.ToLowerInvariant(), CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Equal([code], sessions.ActiveCodes);
        Assert.True(sessions.IsActive(code.ToLowerInvariant()));
    }

    [Theory]
    [InlineData(GameCallStatus.GameFull)]
    [InlineData(GameCallStatus.GameAlreadyStarted)]
    [InlineData(GameCallStatus.NotFound)]
    public async Task JoinAsync_Refused_TracksNothing(GameCallStatus status)
    {
        client.Setup(c => c.JoinAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<JoinGameResponse>.Problem(status, "no"));

        var result = await sessions.JoinAsync(code, CancellationToken.None);

        Assert.Equal(status, result.Status);
        Assert.Empty(sessions.ActiveCodes);
    }

    [Fact]
    public async Task ReconcileAsync_GameEnded_RemovesTheSession()
    {
        sessions.Track(code);
        client.Setup(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code, GameState.Completed, GameViewFactory.FullFleet(), ready: true)));

        await sessions.ReconcileAsync(code, CancellationToken.None);

        Assert.Empty(sessions.ActiveCodes);
    }

    [Fact]
    public async Task ReconcileAsync_UntrackedGame_IsIgnored()
    {
        await sessions.ReconcileAsync(code, CancellationToken.None);

        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReconcileAllAsync_ReconcilesEveryTrackedGame()
    {
        var other = GameViewFactory.Code();
        sessions.Track(code);
        sessions.Track(other);
        client.Setup(c => c.GetGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string c, CancellationToken _) => GameCallResult<GameView>.Ok(
                GameViewFactory.View(c, GameState.Started, GameViewFactory.FullFleet(), ready: true)));

        await sessions.ReconcileAllAsync(CancellationToken.None);

        client.Verify(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetGameAsync(other, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, sessions.ActiveCodes.Count);
    }

    [Fact]
    public async Task TriggerReconcile_RunsInTheBackground()
    {
        sessions.Track(code);
        client.Setup(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Problem(GameCallStatus.NotFound, "gone"));

        sessions.TriggerReconcile(code);
        await sessions.LastBackgroundTask!;

        Assert.Empty(sessions.ActiveCodes);
    }

    [Fact]
    public async Task TriggerReconcile_Throwing_IsLoggedAndSwallowed()
    {
        sessions.Track(code);
        client.Setup(c => c.GetGameAsync(code, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));

        sessions.TriggerReconcile(code);
        await sessions.LastBackgroundTask!;

        Assert.Equal([code], sessions.ActiveCodes);
    }
}

internal static class SessionsFactory
{
    public static GameSessions Create(IGamesClient client, BotSettings settings, TimeProvider timeProvider) =>
        new(client, settings, timeProvider, NullLoggerFactory.Instance, Mock.Of<IHostApplicationLifetime>())
        {
            RandomFor = _ => new Random(11),
        };
}
