using Bogus;
using HexMaster.Battleship.IntegrationEvents.Games;
using HexMaster.Battleship.IntegrationEvents.Players;

namespace HexMaster.Battleship.IntegrationEvents.Tests.Factories;

// Builds plausible catalog events; tests override only the parts they assert on.
public static class IntegrationEventFactory
{
    private static readonly Faker Faker = new();

    public static string GameCode() => Faker.Random.String2(8, "ABCDEFGHJKLMNPQRSTUVWXYZ23456789");

    public static PlayerCreatedEvent PlayerCreated(Guid? playerId = null) =>
        new(playerId ?? Guid.NewGuid(), Faker.Internet.UserName());

    public static PlayerUpdatedEvent PlayerUpdated() =>
        new(Guid.NewGuid(), Faker.Internet.UserName());

    public static GameCreatedEvent GameCreated() =>
        new(Guid.NewGuid(), GameCode(), Guid.NewGuid());

    public static GameJoinedEvent GameJoined() =>
        new(Guid.NewGuid(), GameCode(), Guid.NewGuid(), Faker.Internet.UserName());

    public static GamePlayerReadyEvent PlayerReady() =>
        new(Guid.NewGuid(), GameCode(), Guid.NewGuid());

    public static GameStartedEvent GameStarted() =>
        new(Guid.NewGuid(), GameCode(), Guid.NewGuid());

    public static GameShotFiredEvent ShotFired(string? gameCode = null, Guid? playerId = null, int? column = null, int? row = null) =>
        new(
            Guid.NewGuid(),
            gameCode ?? GameCode(),
            playerId ?? Guid.NewGuid(),
            column ?? Faker.Random.Int(0, 9),
            row ?? Faker.Random.Int(0, 9));

    public static GameEndedEvent GameEnded(GameEndReason? reason = null, Guid? winnerId = null) =>
        new(Guid.NewGuid(), GameCode(), reason ?? GameEndReason.Completed, winnerId);
}
