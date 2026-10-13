using System.Diagnostics;
using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Observability;
using HexMaster.Battleship.Events.Replay;
using HexMaster.Battleship.Events.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Routing;

public sealed class EventRouter(
    IConnectionRegistry registry,
    IReplayBuffer replayBuffer,
    IOptions<StreamOptions> streamOptions,
    EventsMetrics metrics,
    ILogger<EventRouter> logger) : IEventRouter
{
    private long _sequence;

    // Routing and connecting are serialised so every connection sees events in sequence
    // order, and a reconnecting client gets each missed event exactly once: either from the
    // replay or live, never both.
    private readonly Lock _lock = new();

    public EventConnection Connect(string playerId, long? lastEventId)
    {
        var connection = new EventConnection(playerId, streamOptions.Value.ChannelCapacity, logger, metrics);
        var replayed = 0;

        lock (_lock)
        {
            registry.Register(connection);

            if (lastEventId is { } lastId)
            {
                foreach (var item in replayBuffer.Since(lastId, playerId))
                {
                    if (connection.TryWrite(item))
                    {
                        replayed++;
                    }
                }
            }
        }

        metrics.ConnectionOpened();
        metrics.Replayed(replayed);
        EventsLog.StreamOpened(logger, connection.Id, playerId, lastEventId, replayed);

        return connection;
    }

    public void Disconnect(EventConnection connection)
    {
        registry.Unregister(connection);

        if (connection.TryMarkClosed())
        {
            metrics.ConnectionClosed();
            EventsLog.StreamClosed(logger, connection.Id, connection.PlayerId);
        }
    }

    public RouteResult Route(IntegrationEventEnvelope envelope)
    {
        // One span per routed event rather than per connection, so a broadcast to many players
        // does not flood the trace.
        using var activity = EventsTelemetry.ActivitySource.StartActivity(EventsTelemetry.DeliverActivityName);
        activity?.SetTag(EventsTelemetry.EventTypeAttribute, envelope.Type);

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
                metrics.Dropped(DropReasons.ConnectionClosed);
                activity?.AddEvent(new ActivityEvent(
                    EventsTelemetry.ConnectionRemovedEventName,
                    tags: new ActivityTagsCollection { [EventsTelemetry.ConnectionIdAttribute] = connection.Id.ToString() }));
                EventsLog.ClosedStreamRemoved(logger, connection.Id, connection.PlayerId);
                registry.Unregister(connection);
            }

            metrics.Delivered(envelope.Type, delivered);
            activity?.SetTag(EventsTelemetry.EventSequenceAttribute, sequence);
            activity?.SetTag(EventsTelemetry.ConnectionCountAttribute, delivered);
            activity?.SetStatus(ActivityStatusCode.Ok);

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
