using System.Diagnostics;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Games.Services;

// Optimistic concurrency over the stored game's ETag: read, mutate, store if unchanged, otherwise
// read again and re-apply. Retries are immediate; two players per game rarely collide, and a delay
// would only slow down the loser of a race.
public sealed class GameUpdater(
    IGameRepository games,
    GamesMetrics metrics,
    ILogger<GameUpdater> logger) : IGameUpdater
{
    public const int MaxAttempts = 3;

    public async Task<GameUpdateResult<T>> UpdateAsync<T>(
        GameCode code,
        Func<Game, GameMutation<T>> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (await games.GetForUpdateAsync(code.Value, cancellationToken) is not { } stored)
            {
                return GameUpdateResult<T>.NotFound;
            }

            var game = stored.Game as Game
                ?? throw new InvalidOperationException($"The repository returned a {stored.Game.GetType().Name}, not a Game.");
            var mutation = mutate(game);
            if (!mutation.HasChanges)
            {
                return GameUpdateResult<T>.Completed(mutation.Value, saved: false);
            }

            if (await games.TryUpdateAsync(game, stored.ConcurrencyToken, cancellationToken))
            {
                return GameUpdateResult<T>.Completed(mutation.Value, saved: true);
            }

            Activity.Current?.AddEvent(new ActivityEvent(
                GamesTelemetry.UpdateConflictEvent,
                tags: new ActivityTagsCollection { [GamesTelemetry.UpdateAttemptAttribute] = attempt }));
            metrics.UpdateConflicted();
            GamesLog.GameUpdateConflicted(logger, code.Value, attempt, MaxAttempts);
        }

        throw new GameUpdateConflictException(code.Value, MaxAttempts);
    }
}
