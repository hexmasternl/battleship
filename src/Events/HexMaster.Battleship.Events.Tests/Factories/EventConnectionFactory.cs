using Bogus;
using HexMaster.Battleship.Events.Connections;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.Battleship.Events.Tests.Factories;

public static class EventConnectionFactory
{
    private static readonly Faker Faker = new();

    public static EventConnection Create(string? playerId = null, int capacity = 256) =>
        new(playerId ?? Faker.Random.AlphaNumeric(8), capacity, NullLogger.Instance);
}
