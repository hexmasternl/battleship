using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Users.Abstractions.DomainModels;

// A refresh token, identified by the Base64Url SHA-256 hash of its value; the value itself is
// never stored.
public interface IRefreshToken : IDomainModel<string>
{
    Guid UserId { get; }

    DateTimeOffset IssuedAt { get; }

    DateTimeOffset ExpiresAt { get; }

    // Set when the token was exchanged for a new pair; a consumed token is never accepted again.
    DateTimeOffset? ConsumedAt { get; }

    bool IsActive(DateTimeOffset now);

    // Marks the token as used. Throws a DomainException when it is consumed or expired already.
    void Consume(DateTimeOffset now);
}
