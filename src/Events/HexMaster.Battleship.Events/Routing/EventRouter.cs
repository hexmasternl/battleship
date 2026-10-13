using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Replay;
using HexMaster.Battleship.Events.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Routing;

public sealed class EventRouter(
    IConnectionRegistry registry,
    IReplayBuffer replayBuffer,
    IOptions<StreamOptions> streamOptions,
    ILogger<EventRouter> logger) : IEventRouter
{
    private long _sequence;

    // Routing and connecting are serialised so every connection sees events in sequence
    // order, and a reconnecting client gets each missed event exactly once: either from the
    // replay or live, never both.
    private readonly Lock _lock = new();

    public EventConnection Connect(string playerId, long? lastEventId)
    {
        var connection = new EventConnection(playerId, streamOptions.Value.ChannelCapacity, logger);

        lock (_lock)
        {
            registry.Register(connection);

            if (lastEventId is { } lastId)
            {
                foreach (var item in replayBuffer.Since(lastId, playerId))
                {
                    connection.TryWrite(item);
                }
            }
        }

        logger.LogInformation(
            "Opened event stream {ConnectionId} for player {PlayerId} after event {LastEventId}",
            connection.Id, playerId, lastEventId);

        return connection;
    }

    public void Disconnect(EventConnection connection)
    {
        registry.Unregister(connection);

        logger.LogInformation(
            "Closed event stream {ConnectionId} of player {PlayerId}",
            connection.Id, connection.PlayerId);
    }

    public RouteResult Route(IntegrationEventEnvelope envelope)
    {
        lock (_lock)
        {
            var sequence = Interlocked.Increment(ref _sequence);
            var item = SseItems.FromEnvelope(sequence, envelope);
            var delivered = 0;

            replayBuffer.Append(sequence, envelope, item);

            foreach (var connection in TargetsOf(envelope))
            {
                if (connection.TryWrite(item))
                {
                    delivered++;
                    continue;
                }

                // A completed connection has gone away without unregistering; drop it and
                // carry on with the others.
                logger.LogInformation(
                    "Removing closed event stream {ConnectionId} of player {PlayerId}",
                    connection.Id, connection.PlayerId);
                registry.Unregister(connection);
            }

            return new RouteResult(sequence, delivered);
        }
    }

    private IEnumerable<EventConnection> TargetsOf(IntegrationEventEnvelope envelope) =>
        envelope.Target switch
        {
            EventTarget.All => registry.AllConnections(),
            EventTarget.Player or EventTarget.Game => envelope.TargetIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .SelectMany(registry.ConnectionsFor),
            _ => []
        };
}
