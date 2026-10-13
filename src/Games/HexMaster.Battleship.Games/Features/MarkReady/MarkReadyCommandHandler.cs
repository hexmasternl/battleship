using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Features.GetGame;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Games.Features.MarkReady;

// Returns the caller's view after getting ready, or null when no game has the code or the caller
// does not play in it. Refusals surface as PlacementRefusedException, conflicts as
// GameUpdateConflictException.
//
// Two players getting ready at once is safe: each first attempt sees the other not yet ready, the
// first write wins, and the loser's retry re-runs MarkReady on the stored state, now sees both ready
// and starts the game. Only that one stored update starts it, so game.started.v1 is sent once.
public sealed class MarkReadyCommandHandler(
    IGameUpdater updater,
    IFirstTurnPicker picker,
    IIntegrationEventSender events,
    GamesMetrics metrics,
    TimeProvider time,
    ILogger<MarkReadyCommandHandler> logger) : ICommandHandler<MarkReadyCommand, GameView?>
{
    public async Task<GameView?> Handle(MarkReadyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The use-case span comes from the handler decorator; this handler only adds attributes.
        var activity = Activity.Current;
        activity?.SetTag(GamesTelemetry.GameCodeAttribute, command.Code.Value);
        activity?.SetTag(GamesTelemetry.PlayerIdAttribute, command.PlayerId.ToString());

        var result = await updater.UpdateAsync(command.Code, game => Ready(game, command), cancellationToken);
        if (!result.Found || result.Value is not { } ready)
        {
            activity?.SetTag(GamesTelemetry.ReadyOutcomeAttribute, "not-found");
            return null;
        }

        activity?.SetTag(GamesTelemetry.GameIdAttribute, ready.View.GameId.ToString());
        activity?.SetTag(GamesTelemetry.ReadyOutcomeAttribute, ready.Outcome switch
        {
            ReadyOutcome.AlreadyReady => "already-ready",
            ReadyOutcome.Ready => "ready",
            _ => "ready-and-started",
        });

        // Events only come from the attempt that stored the change, so a retry never sends twice.
        if (result.Saved)
        {
            metrics.PlayerReady();
            GamesLog.PlayerReady(logger, ready.View.GameId, command.Code.Value, command.PlayerId);
            if (ready.Outcome == ReadyOutcome.ReadyAndStarted)
            {
                metrics.GameStarted();
                GamesLog.GameStarted(logger, ready.View.GameId, command.Code.Value, ready.PlayerWithTurnId!.Value);
            }

            await AnnounceAsync(ready, command, cancellationToken);
        }

        return ready.View;
    }

    // Runs again on fresh state after a conflict, so it only reads the game it is given.
    private GameMutation<ReadySnapshot?> Ready(Game game, MarkReadyCommand command)
    {
        if (!game.IsPlayer(command.PlayerId))
        {
            return GameMutation.NoChange<ReadySnapshot?>(null);
        }

        var outcome = game.MarkReady(command.PlayerId, picker, time.GetUtcNow());
        var playerIds = game.PlayerTwo is { } playerTwo
            ? new[] { game.PlayerOne.PlayerId, playerTwo.PlayerId }
            : [game.PlayerOne.PlayerId];
        var withTurn = game.PlayerOne.HasTurn ? game.PlayerOne.PlayerId
            : game.PlayerTwo is { HasTurn: true } p ? p.PlayerId
            : (Guid?)null;
        var snapshot = new ReadySnapshot(GameViewMapper.ToView(game, command.PlayerId), outcome, playerIds, withTurn);

        return outcome == ReadyOutcome.AlreadyReady ? GameMutation.NoChange<ReadySnapshot?>(snapshot) : GameMutation.Save<ReadySnapshot?>(snapshot);
    }

    // The ready state is stored; a broker hiccup must not fail the player's request. The other page
    // catches up on its next reload, so a failure is logged and the ready stands.
    private async Task AnnounceAsync(ReadySnapshot ready, MarkReadyCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var audience = EventAudience.Game(ready.PlayerIds);
            await events.SendAsync(
                new GamePlayerReadyEvent(ready.View.GameId, command.Code.Value, command.PlayerId), audience, cancellationToken);

            if (ready.Outcome == ReadyOutcome.ReadyAndStarted)
            {
                await events.SendAsync(
                    new GameStartedEvent(ready.View.GameId, command.Code.Value, ready.PlayerWithTurnId!.Value), audience, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            GamesLog.PlayerReadyAnnouncementFailed(logger, exception, command.Code.Value);
        }
    }

    private sealed record ReadySnapshot(GameView View, ReadyOutcome Outcome, IReadOnlyList<Guid> PlayerIds, Guid? PlayerWithTurnId);
}
