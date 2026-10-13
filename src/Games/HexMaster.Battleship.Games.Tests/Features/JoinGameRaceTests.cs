using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.JoinGame;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.Games.Tests.Fakes;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Features;

// Two players race for the second seat of one game, each through its own handler, as two requests would.
public class JoinGameRaceTests
{
    [Fact]
    public async Task TwoSimultaneousJoins_SeatExactlyOnePlayer()
    {
        var games = new InMemoryGameRepository();
        var game = GameFactory.Stored(GameState.New, withoutPlayerTwo: true);
        await games.TryAddAsync(game, CancellationToken.None);

        // Both requests read the same stored version before either writes.
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
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var results = await Task.WhenAll(
            JoinAsync(games, events.Object, game.Code, b),
            JoinAsync(games, events.Object, game.Code, c));

        var winner = Assert.Single(results, r => r.Error is null);
        var loser = Assert.Single(results, r => r.Error is not null);
        Assert.Equal(GameJoinRefusedReason.GameFull, Assert.IsType<GameJoinRefusedException>(loser.Error).Reason);
        var stored = await games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(winner.PlayerId, stored!.PlayerTwo!.PlayerId);
        events.Verify(e => e.SendAsync(
            It.Is<GameJoinedEvent>(joined => joined.PlayerId == winner.PlayerId),
            It.IsAny<EventAudience>(),
            It.IsAny<CancellationToken>()), Times.Once);
        events.VerifyNoOtherCalls();
    }

    private static async Task<(Guid PlayerId, Exception? Error)> JoinAsync(
        InMemoryGameRepository games, IIntegrationEventSender events, GameCode code, Guid playerId)
    {
        var telemetry = TelemetryFactory.Create(out _);
        var handler = new JoinGameCommandHandler(
            new GameUpdater(games, telemetry, NullLogger<GameUpdater>.Instance),
            events,
            telemetry,
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<JoinGameCommandHandler>.Instance);

        try
        {
            await handler.Handle(new JoinGameCommand(code, playerId, GameFactory.PlayerName()), CancellationToken.None);
            return (playerId, null);
        }
        catch (Exception exception)
        {
            return (playerId, exception);
        }
    }
}
