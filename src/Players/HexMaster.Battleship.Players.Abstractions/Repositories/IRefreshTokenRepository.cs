using HexMaster.Battleship.Players.Abstractions.DomainModels;

namespace HexMaster.Battleship.Players.Abstractions.Repositories;

public interface IRefreshTokenRepository
{
    Task<IRefreshToken?> GetAsync(string hash, CancellationToken cancellationToken);

    // Writes what the token's state asks for. Updating a token that changed in the store since it
    // was read throws a ConcurrencyConflictException, so a token can be consumed only once.
    Task SaveAsync(IRefreshToken token, CancellationToken cancellationToken);
}
