using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Games.Features.FireShot;

// Records the caller's shot and passes the turn. Refusals surface as ShotRefusedException, conflicts
// as GameUpdateConflictException.
//
// A double submit fires once: both requests read the same version, the first write wins, and the
// loser's retry runs FireShot on the stored game, where the opponent now has the turn, so it is
// refused as not-your-turn. Only the stored attempt publishes, so the events are never duplicated.
public sealed class FireShotCommandHandler(
    IGameUpdater updater,
    IIntegrationEventSender events,
    GamesMetrics metrics,
    TimeProvider time,
    ILogger<FireShotCommandHandler> logger) : ICommandHandler<FireShotCommand, FireShotResult>
{
    public async Task<FireShotResult> Handle(FireShotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The use-case span comes from the handler decorator; this handler only adds attributes.
        var activity = Activity.Current;
        activity?.SetTag(GamesTelemetry.GameCodeAttribute, command.Code.Value);
        activity?.SetTag(GamesTelemetry.PlayerIdAttribute, command.PlayerId.ToString());

        GameUpdateResult<ShotSnapshot?> result;
        try
        {
            result = await updater.UpdateAsync(command.Code, game => Fire(game, command), cancellationToken);
        }
        catch (ShotRefusedException refused)
        {
            activity?.SetTag(GamesTelemetry.ShotOutcomeAttribute, refused.Reason switch
            {
                ShotRefusedReason.GameNotStarted => "not-started",
                ShotRefusedReason.NotYourTurn => "not-your-turn",
                _ => "cell-already-shot",
            });
            throw;
        }
        catch (GameUpdateConflictException)
        {
            activity?.SetTag(GamesTelemetry.ShotOutcomeAttribute, "conflict");
            throw;
        }

        if (!result.Found || result.Value is not { } shot)
        {
            activity?.SetTag(GamesTelemetry.ShotOutcomeAttribute, "not-found");
            return FireShotResult.NotFound;
        }

        activity?.SetTag(GamesTelemetry.GameIdAttribute, shot.GameId.ToString());
        activity?.SetTag(GamesTelemetry.ShotOutcomeAttribute, "recorded");
        activity?.SetTag(GamesTelemetry.ShotHitAttribute, shot.Outcome.Hit);

        metrics.ShotFired(shot.Outcome.Hit);
        GamesLog.ShotFired(logger, shot.GameId, command.Code.Value, command.PlayerId, shot.Outcome.Hit);
        await AnnounceAsync(shot, command, cancellationToken);

        var at = shot.Outcome.At;
        return FireShotResult.Success(new FireShotResponse(at.Column, at.Row, shot.Outcome.Hit));
    }

    // Runs again on fresh state after a conflict, so it only reads the game it is given. A recorded
    // shot always changes the game, so there is no "no change" path for a player.
    private GameMutation<ShotSnapshot?> Fire(Game game, FireShotCommand command)
    {
        if (!game.IsPlayer(command.PlayerId))
        {
            return GameMutation.NoChange<ShotSnapshot?>(null);
        }

        var outcome = game.FireShot(command.PlayerId, command.At, time.GetUtcNow());
        var playerTwo = game.PlayerTwo!;
        var withTurn = game.PlayerOne.HasTurn ? game.PlayerOne.PlayerId : playerTwo.PlayerId;

        return GameMutation.Save<ShotSnapshot?>(
            new ShotSnapshot(game.Id, outcome, [game.PlayerOne.PlayerId, playerTwo.PlayerId], withTurn));
    }

    // The shot is stored; a broker hiccup must not fail the player's request. Each event is tried on
    // its own, so a failed shot event still lets the turn change through. Pages catch up on reload.
    private async Task AnnounceAsync(ShotSnapshot shot, FireShotCommand command, CancellationToken cancellationToken)
    {
        var audience = EventAudience.Game(shot.PlayerIds);
        var at = shot.Outcome.At;

        await TrySendAsync(
            new GameShotFiredEvent(shot.GameId, command.Code.Value, command.PlayerId, at.Column, at.Row, shot.Outcome.Hit),
            audience, command, cancellationToken);
        await TrySendAsync(
            new GameTurnChangedEvent(shot.GameId, command.Code.Value, shot.PlayerWithTurnId),
            audience, command, cancellationToken);
    }

    private async Task TrySendAsync<TEvent>(
        TEvent integrationEvent, EventAudience audience, FireShotCommand command, CancellationToken cancellationToken)
        where TEvent : class, IIntegrationEvent
    {
        try
        {
            await events.SendAsync(integrationEvent, audience, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            GamesLog.ShotAnnouncementFailed(logger, exception, TEvent.EventType, command.Code.Value);
        }
    }

    private sealed record ShotSnapshot(Guid GameId, ShotOutcome Outcome, IReadOnlyList<Guid> PlayerIds, Guid PlayerWithTurnId);
}
