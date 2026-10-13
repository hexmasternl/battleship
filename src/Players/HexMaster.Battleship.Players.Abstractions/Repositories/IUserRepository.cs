using HexMaster.Battleship.Players.Abstractions.DomainModels;

namespace HexMaster.Battleship.Players.Abstractions.Repositories;

public interface IUserRepository
{
    Task<IUser?> GetAsync(Guid id, CancellationToken cancellationToken);

    // Writes what the user's state asks for: New inserts, Modified updates, Deleted deletes.
    Task SaveAsync(IUser user, CancellationToken cancellationToken);
}
