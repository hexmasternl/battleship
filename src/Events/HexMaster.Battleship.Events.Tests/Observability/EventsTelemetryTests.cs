using System.Diagnostics;
using System.Diagnostics.Metrics;
using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Observability;
using HexMaster.Battleship.Events.Routing;
using HexMaster.Battleship.Events.Streaming;
using HexMaster.Battleship.Events.Tests.Factories;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Tests.Observability;

public class EventsTelemetryTests
{
    private readonly ConnectionRegistry _registry = new();
    private readonly IMeterFactory _meterFactory;
    private readonly EventsMetrics _metrics;
    private readonly EventRouter _router;

    public EventsTelemetryTests()
    {
        _metrics = EventsMetricsFactory.Create(out _meterFactory);
        _router = new EventRouter(
            _registry,
            ReplayBufferFactory.Create(),
            Options.Create(new StreamOptions()),
            _metrics,
            NullLogger<EventRouter>.Instance);
    }

    [Fact]
    public void ConnectionGauge_FollowsOpenAndClosedStreams()
    {
        using var active = Collect(EventsMetrics.ConnectionsActiveName);

        var first = _router.Connect("user-a", null);
        _router.Connect("user-b", null);
        Assert.Equal(2, active.GetMeasurementSnapshot().EvaluateAsCounter());

        _router.Disconnect(first);
        _router.Disconnect(first);

        Assert.Equal(1, active.GetMeasurementSnapshot().EvaluateAsCounter());
    }

    [Fact]
    public void Connect_WithLastEventId_CountsTheReplayedEvents()
    {
        using var replayed = Collect(EventsMetrics.ReplayedName);
        var first = _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));
        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));
        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-b"));

        _router.Connect("user-a", first.Sequence - 1);

        Assert.Equal(2, replayed.GetMeasurementSnapshot().EvaluateAsCounter());
    }

    [Fact]
    public void Route_CountsDeliveriesByEventType()
    {
        using var delivered = Collect(EventsMetrics.DeliveredName);
        Register("user-a");
        Register("user-a");

        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a", "shot-fired"));

        var measurement = Assert.Single(delivered.GetMeasurementSnapshot());
        Assert.Equal(2, measurement.Value);
        Assert.Equal("shot-fired", measurement.Tags[EventsTelemetry.EventTypeAttribute]);
    }

    [Fact]
    public void Route_ClosedConnection_IsCountedAsDroppedAndRecordedOnTheDeliverSpan()
    {
        using var activities = new ActivityCollector();
        using var dropped = Collect(EventsMetrics.DroppedName);
        var broken = Register("user-a");
        Register("user-b");
        broken.Complete();

        var result = _router.Route(IntegrationEventEnvelopeFactory.ForAll());

        var measurement = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(DropReasons.ConnectionClosed, measurement.Tags[EventsTelemetry.ReasonTag]);

        var deliver = activities.Single(EventsTelemetry.DeliverActivityName);
        Assert.Equal(ActivityStatusCode.Ok, deliver.Status);
        Assert.Equal(result.Sequence, deliver.GetTagItem(EventsTelemetry.EventSequenceAttribute));
        Assert.Equal(1, deliver.GetTagItem(EventsTelemetry.ConnectionCountAttribute));
        var removed = Assert.Single(deliver.Events);
        Assert.Equal(EventsTelemetry.ConnectionRemovedEventName, removed.Name);
        Assert.Contains(removed.Tags, t => t.Key == EventsTelemetry.ConnectionIdAttribute && (string?)t.Value == broken.Id.ToString());
    }

    [Fact]
    public void FullChannel_CountsTheDroppedEvent()
    {
        using var dropped = Collect(EventsMetrics.DroppedName);
        var connection = EventConnectionFactory.Create("user-a", capacity: 1, metrics: _metrics);
        _registry.Register(connection);

        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));
        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        var measurement = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(DropReasons.ChannelFull, measurement.Tags[EventsTelemetry.ReasonTag]);
    }

    private MetricCollector<long> Collect(string instrumentName) =>
        new(_meterFactory, EventsTelemetry.Name, instrumentName);

    private EventConnection Register(string userId)
    {
        var connection = EventConnectionFactory.Create(userId, metrics: _metrics);
        _registry.Register(connection);
        return connection;
    }
}
