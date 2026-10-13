using System.Text.Json;
using Bogus;
using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.Events.Tests.Factories;

// Builds plausible envelopes; tests override only the parts they assert on.
public static class IntegrationEventEnvelopeFactory
{
    private static readonly Faker Faker = new();

    public static IntegrationEventEnvelope Create(
        EventTarget target = EventTarget.User,
        IReadOnlyList<string>? targetIds = null,
        string? type = null,
        string? id = null,
        JsonElement? payload = null) =>
        new(
            id ?? Guid.NewGuid().ToString(),
            type ?? Faker.PickRandom("shot-fired", "ships-placed", "game-started"),
            target,
            targetIds ?? (target == EventTarget.All ? [] : [Faker.Random.AlphaNumeric(8)]),
            Faker.Date.RecentOffset(),
            payload ?? JsonSerializer.SerializeToElement(new { x = Faker.Random.Int(0, 9), y = Faker.Random.Int(0, 9) }));

    public static IntegrationEventEnvelope ForUser(string userId, string? type = null) =>
        Create(EventTarget.User, [userId], type);

    public static IntegrationEventEnvelope ForGame(params string[] userIds) =>
        Create(EventTarget.Game, userIds);

    public static IntegrationEventEnvelope ForAll() => Create(EventTarget.All, []);
}
