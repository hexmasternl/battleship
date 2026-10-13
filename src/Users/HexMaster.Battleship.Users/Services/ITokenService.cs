using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Users.Abstractions.DomainModels;

namespace HexMaster.Battleship.Users.Services;

public interface ITokenService
{
    // Issues an access token and a new refresh token for the user. The refresh token model is
    // New and keyed by the hash; the caller stores it.
    IssuedTokens Issue(IUser user, DateTimeOffset now);
}

public sealed record IssuedTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    IRefreshToken StoredRefreshToken)
{
    public TokenPairResponse ToResponse() =>
        new(AccessToken, AccessTokenExpiresAt, RefreshToken, StoredRefreshToken.ExpiresAt);
}
