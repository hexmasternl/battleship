using System.Diagnostics;

namespace HexMaster.Battleship.IntegrationEvents.Observability;

// The tracing source of the integration event sender and the attributes of its producer span,
// named after the OpenTelemetry messaging semantic conventions.
public static class IntegrationEventsTelemetry
{
    public const string Name = "HexMaster.Battleship.IntegrationEvents";

    public static readonly ActivitySource ActivitySource = new(Name);

    public const string MessagingSystemAttribute = "messaging.system";
    public const string DestinationNameAttribute = "messaging.destination.name";
    public const string OperationTypeAttribute = "messaging.operation.type";
    public const string OperationNameAttribute = "messaging.operation.name";
    public const string MessageIdAttribute = "messaging.message.id";

    // The catalog type name; also the metric tag, a closed set.
    public const string EventTypeAttribute = "event.type";

    public const string MessagingSystem = "dapr";
    public const string PublishOperation = "publish";

    public static string PublishActivityName(string topic) => $"{PublishOperation} {topic}";
}
