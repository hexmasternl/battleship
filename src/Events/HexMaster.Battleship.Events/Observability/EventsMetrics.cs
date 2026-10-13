using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.Events.Observability;

// The business metrics of the Events service. No tag carries a player or connection id.
public sealed class EventsMetrics
{
    public const string ConnectionsActiveName = "events.connections.active";
    public const string DeliveredName = "events.delivered";
    public const string DroppedName = "events.dropped";
    public const string ReplayedName = "events.replayed";

    private readonly UpDownCounter<long> _connectionsActive;
    private readonly Counter<long> _delivered;
    private readonly Counter<long> _dropped;
    private readonly Counter<long> _replayed;

    public EventsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(EventsTelemetry.Name);
        _connectionsActive = meter.CreateUpDownCounter<long>(
            ConnectionsActiveName, unit: "{connection}", description: "Open server-sent event streams.");
        _delivered = meter.CreateCounter<long>(
            DeliveredName, unit: "{event}", description: "Events written to an open stream, by event type.");
        _dropped = meter.CreateCounter<long>(
            DroppedName, unit: "{event}", description: "Events that did not reach a stream, by reason.");
        _replayed = meter.CreateCounter<long>(
            ReplayedName, unit: "{event}", description: "Missed events replayed to a reconnecting stream.");
    }

    public void ConnectionOpened() => _connectionsActive.Add(1);

    public void ConnectionClosed() => _connectionsActive.Add(-1);

    public void Delivered(string eventType, int count)
    {
        if (count > 0)
        {
            _delivered.Add(count, new KeyValuePair<string, object?>(EventsTelemetry.EventTypeAttribute, eventType));
        }
    }

    // reason is one of DropReasons.
    public void Dropped(string reason) =>
        _dropped.Add(1, new KeyValuePair<string, object?>(EventsTelemetry.ReasonTag, reason));

    public void Replayed(int count)
    {
        if (count > 0)
        {
            _replayed.Add(count);
        }
    }
}
