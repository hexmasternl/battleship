using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Games.Features.CleanUpInactiveGames;

// Deletes every game whose last activity is strictly before now minus the retention, whatever its
// state. Each delete is conditional on the version that was selected, so a game a player resumes
// during the run is kept, and a game already gone is skipped: running again, or concurrently on
// another replica, is safe. A run cut short by cancellation is finished by the next one.
public sealed class CleanUpInactiveGamesCommandHandler(
    IGameRepository games,
    IOptions<GameRetentionOptions> options,
    GamesMetrics metrics,
    TimeProvider time,
    ILogger<CleanUpInactiveGamesCommandHandler> logger) : ICommandHandler<CleanUpInactiveGamesCommand, CleanUpInactiveGamesResult>
{
    public async Task<CleanUpInactiveGamesResult> Handle(CleanUpInactiveGamesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The use-case span comes from the handler decorator; this handler only adds attributes.
        var activity = Activity.Current;
        var cutoff = time.GetUtcNow() - options.Value.Retention;
        activity?.SetTag(GamesTelemetry.RetentionCutoffAttribute, cutoff.ToString("O"));

        var removed = 0;
        var skipped = 0;
        await foreach (var inactive in games.GetInactiveAsync(cutoff, cancellationToken))
        {
            var outcome = await games.TryDeleteAsync(inactive.GameCode, inactive.ConcurrencyToken, cancellationToken);
            if (outcome == DeleteOutcome.Deleted)
            {
                removed++;
            }
            else
            {
                skipped++;
            }
        }

        activity?.SetTag(GamesTelemetry.RetentionRemovedAttribute, removed);
        activity?.SetTag(GamesTelemetry.RetentionSkippedAttribute, skipped);
        metrics.GamesRemoved(removed);
        GamesLog.InactiveGamesRemoved(logger, removed, skipped, cutoff);

        return new CleanUpInactiveGamesResult(removed, skipped);
    }
}
