using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Users.Abstractions.DomainModels;

// An anonymous user, identified by the Guid that is the access token's subject.
public interface IUser : IDomainModel<Guid>
{
    string DisplayName { get; }

    DateTimeOffset CreatedAt { get; }

    DateTimeOffset LastSeenAt { get; }

    bool IsRevoked { get; }

    // Records that the user came back at the given instant.
    void Touch(DateTimeOffset now);

    // Stops the user from refreshing tokens.
    void Revoke();
}
