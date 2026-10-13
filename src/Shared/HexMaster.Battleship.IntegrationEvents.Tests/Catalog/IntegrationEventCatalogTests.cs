using System.Text.RegularExpressions;

namespace HexMaster.Battleship.IntegrationEvents.Tests.Catalog;

public partial class IntegrationEventCatalogTests
{
    [GeneratedRegex(@"^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*\.v[1-9][0-9]*$")]
    private static partial Regex TypeNameFormat();

    [Fact]
    public void Events_ContainsTheV1Catalog()
    {
        var names = IntegrationEventCatalog.Events.Select(e => e.EventType);

        Assert.Equal(
            [
                "user.created.v1",
                "user.updated.v1",
                "game.created.v1",
                "game.joined.v1",
                "game.player-ready.v1",
                "game.started.v1",
                "game.shot-fired.v1",
                "game.turn-changed.v1",
                "game.ended.v1"
            ],
            names);
    }

    [Fact]
    public void Events_TypeNamesFollowTheVersionedFormat()
    {
        Assert.All(IntegrationEventCatalog.Events, e => Assert.Matches(TypeNameFormat(), e.EventType));
    }

    [Fact]
    public void Events_TypeNamesAreUnique()
    {
        var names = IntegrationEventCatalog.Events.Select(e => e.EventType).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Events_ListsEveryIntegrationEventInTheAssembly()
    {
        var implementations = typeof(IIntegrationEvent).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(t))
            .ToHashSet();

        var catalogued = IntegrationEventCatalog.Events.Select(e => e.ClrType).ToHashSet();

        Assert.Equal(implementations.OrderBy(t => t.FullName), catalogued.OrderBy(t => t.FullName));
    }
}
