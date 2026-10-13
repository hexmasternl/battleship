using System.Collections.Concurrent;

namespace HexMaster.Battleship.Events.Connections;

// In-process registry: one Events API instance only sees its own connections.
public sealed class ConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, EventConnection>> _byUser = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, EventConnection> _all = new();
    private readonly Lock _lock = new();

    public void Register(EventConnection connection)
    {
        // The lock keeps a user's inner dictionary from being removed while it is reused.
        lock (_lock)
        {
            _byUser.GetOrAdd(connection.UserId, _ => new()).TryAdd(connection.Id, connection);
            _all.TryAdd(connection.Id, connection);
        }
    }

    public void Unregister(EventConnection connection)
    {
        lock (_lock)
        {
            _all.TryRemove(connection.Id, out _);

            if (_byUser.TryGetValue(connection.UserId, out var connections)
                && connections.TryRemove(connection.Id, out _)
                && connections.IsEmpty)
            {
                _byUser.TryRemove(connection.UserId, out _);
            }
        }

        connection.Complete();
    }

    public IReadOnlyCollection<EventConnection> ConnectionsFor(string userId) =>
        _byUser.TryGetValue(userId, out var connections) ? [.. connections.Values] : [];

    public IReadOnlyCollection<EventConnection> AllConnections() => [.. _all.Values];
}
