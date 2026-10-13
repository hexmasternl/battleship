using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Features.MarkReady;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.Games.Tests.Fakes;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Features;

// Both players press ready at the same moment, each through its own handler, as two requests would.
public class MarkReadyRaceTests
{
    [Fact]
    public async Task TwoSimultaneousReadies_StartTheGameExactlyOnce()
    {
        var games = new InMemoryGameRepository();
        var game = GameFactory.Stored(
            GameState.New,
            playerOne: GameFactory.Player(ships: GameFactory.FullFleet()),
            playerTwo: GameFactory.Player(ships: GameFactory.FullFleet()));
        await games.TryAddAsync(game, CancellationToken.None);

        // Both requests read the same stored version, where nobody is ready, before either writes.
        var reads = 0;
        var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        games.AfterRead = async () =>
        {
            if (Interlocked.Increment(ref reads) <= 2)
            {
                if (reads == 2)
                {
                    bothRead.SetResult();
                }

                await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        };

        var events = new Mock<IIntegrationEventSender>();

        var views = await Task.WhenAll(
            ReadyAsync(games, events.Object, game.Code, game.PlayerOne.PlayerId),
            ReadyAsync(games, events.Object, game.Code, game.PlayerTwo!.PlayerId));

        Assert.All(views, view => Assert.True(view!.Me.Ready));
        var stored = await games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(GameState.Started, stored!.GameState);
        Assert.True(stored.PlayerOne.IsReady && stored.PlayerTwo!.IsReady);
        Assert.True(stored.PlayerOne.HasTurn ^ stored.PlayerTwo!.HasTurn);
        events.Verify(e => e.SendAsync(It.IsAny<GamePlayerReadyEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        events.Verify(e => e.SendAsync(It.IsAny<GameStartedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()), Times.Once);
        events.VerifyNoOtherCalls();
    }

    private static Task<Abstractions.DataTransferObjects.GameView?> ReadyAsync(
        InMemoryGameRepository games, IIntegrationEventSender events, GameCode code, Guid playerId)
    {
        var telemetry = TelemetryFactory.Create(out _);
        var handler = new MarkReadyCommandHandler(
            new GameUpdater(games, telemetry, NullLogger<GameUpdater>.Instance),
            new RandomFirstTurnPicker(),
            events,
            telemetry,
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<MarkReadyCommandHandler>.Instance);

        return handler.Handle(new MarkReadyCommand(code, playerId), CancellationToken.None);
    }
}
