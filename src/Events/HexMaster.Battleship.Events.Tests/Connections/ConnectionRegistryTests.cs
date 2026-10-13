using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Tests.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.Battleship.Events.Tests.Connections;

public class ConnectionRegistryTests
{
    private readonly ConnectionRegistry _registry = new();

    [Fact]
    public void ConnectionsFor_PlayerWithTwoConnections_ReturnsBoth()
    {
        var first = EventConnectionFactory.Create("player-a");
        var second = EventConnectionFactory.Create("player-a");
        var other = EventConnectionFactory.Create("player-b");

        _registry.Register(first);
        _registry.Register(second);
        _registry.Register(other);

        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            _registry.ConnectionsFor("player-a").Select(c => c.Id).Order());
        Assert.Equal(3, _registry.AllConnections().Count);
    }

    [Fact]
    public void Unregister_OneOfTwoConnections_KeepsTheOther()
    {
        var first = EventConnectionFactory.Create("player-a");
        var second = EventConnectionFactory.Create("player-a");
        _registry.Register(first);
        _registry.Register(second);

        _registry.Unregister(first);

        Assert.Equal([second], _registry.ConnectionsFor("player-a"));
        Assert.Equal([second], _registry.AllConnections());
    }

    [Fact]
    public void Unregister_LastConnection_RemovesPlayerAndCompletesConnection()
    {
        var connection = EventConnectionFactory.Create("player-a");
        _registry.Register(connection);

        _registry.Unregister(connection);
        _registry.Unregister(connection);

        Assert.Empty(_registry.ConnectionsFor("player-a"));
        Assert.Empty(_registry.AllConnections());
        Assert.True(connection.Reader.Completion.IsCompleted);
    }

    [Fact]
    public void ConnectionsFor_UnknownPlayer_IsEmpty()
    {
        Assert.Empty(_registry.ConnectionsFor("nobody"));
    }

    [Fact]
    public void AddEventsCore_RegistersRegistryAsSingleton()
    {
        using var provider = new ServiceCollection().AddEventsCore().BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IConnectionRegistry>(), provider.GetRequiredService<IConnectionRegistry>());
    }
}
