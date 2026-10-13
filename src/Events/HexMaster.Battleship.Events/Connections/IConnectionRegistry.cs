namespace HexMaster.Battleship.Events.Connections;

// The open event streams of this process, by player.
public interface IConnectionRegistry
{
    void Register(EventConnection connection);

    // Removes and completes the connection. Safe to call more than once.
    void Unregister(EventConnection connection);

    IReadOnlyCollection<EventConnection> ConnectionsFor(string playerId);

    IReadOnlyCollection<EventConnection> AllConnections();
}
