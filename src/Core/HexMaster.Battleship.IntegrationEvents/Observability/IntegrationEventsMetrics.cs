using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.IntegrationEvents.Observability;

// Publishing outcomes by catalog event type.
public sealed class IntegrationEventsMetrics
{
    public const string PublishedName = "integration_events.published";
    public const string PublishFailedName = "integration_events.publish_failed";

    private readonly Counter<long> _published;
    private readonly Counter<long> _publishFailed;

    public IntegrationEventsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(IntegrationEventsTelemetry.Name);
        _published = meter.CreateCounter<long>(
            PublishedName, unit: "{event}", description: "Integration events handed to the Dapr sidecar, by event type.");
        _publishFailed = meter.CreateCounter<long>(
            PublishFailedName, unit: "{event}", description: "Integration events that could not be published, by event type.");
    }

    public void Published(string eventType) => _published.Add(1, EventTypeTag(eventType));

    public void PublishFailed(string eventType) => _publishFailed.Add(1, EventTypeTag(eventType));

    private static KeyValuePair<string, object?> EventTypeTag(string eventType) =>
        new(IntegrationEventsTelemetry.EventTypeAttribute, eventType);
}
