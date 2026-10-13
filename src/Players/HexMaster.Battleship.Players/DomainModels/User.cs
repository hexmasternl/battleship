using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Abstractions.DomainModels;

namespace HexMaster.Battleship.Players.DomainModels;

public sealed class User : DomainModel<Guid>, IUser
{
    private User(
        Guid id,
        string displayName,
        DateTimeOffset createdAt,
        DateTimeOffset lastSeenAt,
        bool isRevoked,
        DomainModelState state)
        : base(id, state)
    {
        DisplayName = displayName;
        CreatedAt = createdAt;
        LastSeenAt = lastSeenAt;
        IsRevoked = isRevoked;
    }

    public string DisplayName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public bool IsRevoked { get; private set; }

    // A new user with a fresh identifier, last seen at the moment it was created.
    public static User Create(string displayName, DateTimeOffset now) =>
        new(Guid.NewGuid(), ValidateDisplayName(displayName), now, now, false, DomainModelState.New);

    public static IUser Reconcile(
        Guid id,
        string displayName,
        DateTimeOffset createdAt,
        DateTimeOffset lastSeenAt,
        bool isRevoked) =>
        new User(id, displayName, createdAt, lastSeenAt, isRevoked, DomainModelState.Pristine);

    public void Touch(DateTimeOffset now) => Change(LastSeenAt, now, v => LastSeenAt = v);

    public void Revoke() => Change(IsRevoked, true, v => IsRevoked = v);

    private static string ValidateDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException("A display name is required.");
        }

        if (trimmed.Length > DisplayNameRules.MaxLength)
        {
            throw new DomainException($"A display name must be {DisplayNameRules.MaxLength} characters or fewer.");
        }

        return trimmed;
    }
}
