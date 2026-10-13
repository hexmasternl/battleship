using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DomainModels;

namespace HexMaster.Battleship.Players.DomainModels;

public sealed class RefreshToken : DomainModel<string>, IRefreshToken
{
    private RefreshToken(
        string hash,
        Guid userId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        DomainModelState state)
        : base(hash, state)
    {
        UserId = userId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
    }

    public Guid UserId { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    // A new token for the user, keyed by the hash of its value and expiring after the lifetime.
    public static RefreshToken Create(string hash, Guid userId, DateTimeOffset issuedAt, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new DomainException("A refresh token needs the hash of its value.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("A refresh token belongs to a user.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("A refresh token lifetime must be positive.");
        }

        return new RefreshToken(hash, userId, issuedAt, issuedAt + lifetime, null, DomainModelState.New);
    }

    public static IRefreshToken Reconcile(
        string hash,
        Guid userId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt) =>
        new RefreshToken(hash, userId, issuedAt, expiresAt, consumedAt, DomainModelState.Pristine);

    public bool IsActive(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public void Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
        {
            throw new DomainException("The refresh token was used already.");
        }

        if (now >= ExpiresAt)
        {
            throw new DomainException("The refresh token has expired.");
        }

        Change(ConsumedAt, now, v => ConsumedAt = v);
    }
}
