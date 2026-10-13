using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Streaming;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Events.Routing;

public sealed class EventRouter(IConnectionRegistry registry, ILogger<EventRouter> logger) : IEventRouter
{
    private long _sequence;

    // Routing is serialised so every connection sees events in sequence order.
    private readonly Lock _lock = new();

    public RouteResult Route(IntegrationEventEnvelope envelope)
    {
        lock (_lock)
        {
            var sequence = Interlocked.Increment(ref _sequence);
            var item = SseItems.FromEnvelope(sequence, envelope);
            var delivered = 0;

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
