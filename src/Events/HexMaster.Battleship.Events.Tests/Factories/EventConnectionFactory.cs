using Bogus;
using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.Battleship.Events.Tests.Factories;

public static class EventConnectionFactory
{
    private static readonly Faker Faker = new();

    public static EventConnection Create(string? userId = null, int capacity = 256, EventsMetrics? metrics = null) =>
        new(userId ?? Faker.Random.AlphaNumeric(8), capacity, NullLogger.Instance, metrics ?? EventsMetricsFactory.Create());
}
