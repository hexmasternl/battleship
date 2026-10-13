using Bogus;
using HexMaster.Battleship.Users.Abstractions.DomainModels;
using HexMaster.Battleship.Users.DomainModels;

namespace HexMaster.Battleship.Users.Tests.Factories;

// Builds stored refresh tokens; by default issued a day ago and valid for 90 days.
public static class RefreshTokenFactory
{
    private static readonly Faker Faker = new();

    public static string Hash() => Faker.Random.AlphaNumeric(43);

    public static IRefreshToken Stored(
        DateTimeOffset now,
        string? hash = null,
        Guid? userId = null,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? consumedAt = null)
    {
        var issued = issuedAt ?? now.AddDays(-1);
        return RefreshToken.Reconcile(
            hash ?? Hash(),
            userId ?? Guid.NewGuid(),
            issued,
            expiresAt ?? issued.AddDays(90),
            consumedAt);
    }
}
