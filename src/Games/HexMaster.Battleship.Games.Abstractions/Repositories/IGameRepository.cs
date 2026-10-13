using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Abstractions.Repositories;

public interface IGameRepository
{
    // Stores a new game under its code. Returns false, and stores nothing, when a game with that
    // code already exists; the caller then picks another code.
    Task<bool> TryAddAsync(IGame game, CancellationToken cancellationToken);

    Task<IGame?> GetByCodeAsync(string gameCode, CancellationToken cancellationToken);
}
