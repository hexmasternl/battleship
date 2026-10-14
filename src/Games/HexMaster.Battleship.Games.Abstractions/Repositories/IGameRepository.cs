using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Abstractions.Repositories;

public interface IGameRepository
{
    // Stores a new game under its code. Returns false, and stores nothing, when a game with that
    // code already exists; the caller then picks another code.
    Task<bool> TryAddAsync(IGame game, CancellationToken cancellationToken);

    Task<IGame?> GetByCodeAsync(string gameCode, CancellationToken cancellationToken);

    // Reads a game together with the concurrency token of its stored version; null when unknown.
    Task<GameForUpdate?> GetForUpdateAsync(string gameCode, CancellationToken cancellationToken);

    // Replaces the stored game only when it still has the version the token names. Returns false,
    // and stores nothing, when another update was stored in between; the caller reads it again.
    Task<bool> TryUpdateAsync(IGame game, string concurrencyToken, CancellationToken cancellationToken);

    // Streams the games whose last activity is strictly before the cutoff, by code and version token
    // only: the stored game itself is never read, so an unreadable game is still selected.
    IAsyncEnumerable<InactiveGame> GetInactiveAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    // Deletes the stored game only when it still has the version the token names.
    Task<DeleteOutcome> TryDeleteAsync(string gameCode, string concurrencyToken, CancellationToken cancellationToken);
}
