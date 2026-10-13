using Bogus;
using HexMaster.Battleship.Players.Abstractions.DomainModels;
using HexMaster.Battleship.Players.DomainModels;

namespace HexMaster.Battleship.Players.Tests.Factories;

// Builds plausible users; tests override only the parts they assert on.
public static class UserFactory
{
    private static readonly Faker Faker = new();

    public static string DisplayName() => $"{Faker.PickRandom("Admiral", "Captain", "Commodore")} {Faker.Name.LastName()}";

    public static IUser Stored(
        Guid? id = null,
        string? displayName = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? lastSeenAt = null,
        bool isRevoked = false)
    {
        var created = createdAt ?? Faker.Date.PastOffset();
        return User.Reconcile(
            id ?? Guid.NewGuid(),
            displayName ?? DisplayName(),
            created,
            lastSeenAt ?? created,
            isRevoked);
    }
}
