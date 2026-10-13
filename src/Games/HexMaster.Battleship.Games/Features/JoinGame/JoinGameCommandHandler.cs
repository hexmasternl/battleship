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

namespace HexMaster.Battleship.Games.Features.JoinGame;

public sealed class JoinGameCommandHandler(
    IGameUpdater updater,
    IIntegrationEventSender events,
    GamesMetrics metrics,
    TimeProvider time,
    ILogger<JoinGameCommandHandler> logger) : ICommandHandler<JoinGameCommand, JoinGameResult>
{
    public async Task<JoinGameResult> Handle(JoinGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The use-case span comes from the handler decorator; this handler only adds attributes.
        var activity = Activity.Current;
        activity?.SetTag(GamesTelemetry.GameCodeAttribute, command.Code.Value);
        activity?.SetTag(GamesTelemetry.PlayerIdAttribute, command.PlayerId.ToString());

        GameUpdateResult<JoinSnapshot> result;
        try
        {
            result = await updater.UpdateAsync(command.Code, game => Join(game, command), cancellationToken);
        }
        catch (GameJoinRefusedException refused)
        {
            activity?.SetTag(GamesTelemetry.JoinOutcomeAttribute,
                refused.Reason == GameJoinRefusedReason.GameFull ? "full" : "not-joinable");
            throw;
        }

        if (!result.Found || result.Value is not { } joined)
        {
            activity?.SetTag(GamesTelemetry.JoinOutcomeAttribute, "not-found");
            return JoinGameResult.NotFound;
        }

        activity?.SetTag(GamesTelemetry.GameIdAttribute, joined.GameId.ToString());
        if (!result.Saved)
        {
            activity?.SetTag(GamesTelemetry.JoinOutcomeAttribute, "already-player");
            return JoinGameResult.Success(new JoinGameResponse(joined.GameId, command.Code.Value));
        }

        activity?.SetTag(GamesTelemetry.JoinOutcomeAttribute, "joined");
        metrics.GameJoined();
        GamesLog.GameJoined(logger, joined.GameId, command.Code.Value, command.PlayerId);
        await AnnounceAsync(joined, command, cancellationToken);

        return JoinGameResult.Success(new JoinGameResponse(joined.GameId, command.Code.Value));
    }

    // Runs again on fresh state after a conflict, so it only reads the game it is given.
    private GameMutation<JoinSnapshot> Join(Game game, JoinGameCommand command)
    {
        var outcome = game.Join(command.PlayerId, command.PlayerName, time.GetUtcNow());
        var snapshot = new JoinSnapshot(game.Id, game.PlayerOne.PlayerId);

        return outcome == JoinOutcome.Joined ? GameMutation.Save(snapshot) : GameMutation.NoChange(snapshot);
    }

    // The join is stored; a broker hiccup must not fail the player's request. Player one sees the
    // second player after reloading the game, so a failure is logged and the join stands.
    private async Task AnnounceAsync(JoinSnapshot joined, JoinGameCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await events.SendAsync(
                new GameJoinedEvent(joined.GameId, command.Code.Value, command.PlayerId, command.PlayerName.Trim()),
                EventAudience.Game(joined.PlayerOneId, command.PlayerId),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            GamesLog.GameJoinedAnnouncementFailed(logger, exception, GameJoinedEvent.EventType, command.Code.Value);
        }
    }

    // On a stored join the caller is player two, so player one and the caller are the game's audience.
    private sealed record JoinSnapshot(Guid GameId, Guid PlayerOneId);
}
