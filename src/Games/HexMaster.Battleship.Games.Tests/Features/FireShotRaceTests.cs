using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.FireShot;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.Games.Tests.Fakes;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Features;

// The player with the turn submits twice at the same moment, each through its own handler, as two
// requests would.
public class FireShotRaceTests
{
    [Fact]
    public async Task TwoSimultaneousShots_RecordExactlyOne()
    {
        var games = new InMemoryGameRepository();
        var game = FireShotCommandHandlerTests.StartedGame();
        await games.TryAddAsync(game, CancellationToken.None);

        // Both requests read the same stored version, where player one has the turn, before either writes.
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
        var shooter = game.PlayerOne.PlayerId;

        var attempts = await Task.WhenAll(
            AttemptAsync(games, events.Object, game.Code, shooter, new Coordinate(1, 1)),
            AttemptAsync(games, events.Object, game.Code, shooter, new Coordinate(2, 2)));

        Assert.Single(attempts, a => a.Result?.Shot is not null);
        Assert.Equal(ShotRefusedReason.NotYourTurn, Assert.Single(attempts, a => a.Refusal is not null).Refusal);
        var stored = await games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Single(stored!.PlayerOne.Shots);
        Assert.True(stored.PlayerTwo!.HasTurn);
        events.Verify(e => e.SendAsync(It.IsAny<GameShotFiredEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()), Times.Once);
        events.Verify(e => e.SendAsync(It.IsAny<GameTurnChangedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()), Times.Once);
        events.VerifyNoOtherCalls();
    }

    private static async Task<(FireShotResult? Result, ShotRefusedReason? Refusal)> AttemptAsync(
        InMemoryGameRepository games, IIntegrationEventSender events, GameCode code, Guid playerId, Coordinate at)
    {
        var telemetry = TelemetryFactory.Create(out _);
        var handler = new FireShotCommandHandler(
            new GameUpdater(games, telemetry, NullLogger<GameUpdater>.Instance),
            events,
            telemetry,
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<FireShotCommandHandler>.Instance);

        try
        {
            return (await handler.Handle(new FireShotCommand(code, playerId, at), CancellationToken.None), null);
        }
        catch (ShotRefusedException refused)
        {
            return (null, refused.Reason);
        }
    }
}
