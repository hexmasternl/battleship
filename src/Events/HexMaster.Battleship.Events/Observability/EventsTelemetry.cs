using System.Diagnostics;

namespace HexMaster.Battleship.Events.Observability;

// The tracing source of the Events service and the attributes and tags it writes. Messaging
// attributes follow the OpenTelemetry semantic conventions.
public static class EventsTelemetry
{
    public const string Name = "HexMaster.Battleship.Events";

    public static readonly ActivitySource ActivitySource = new(Name);

    public const string DeliverActivityName = "deliver";
    public const string ConnectionRemovedEventName = "connection.removed";

    public const string MessagingSystemAttribute = "messaging.system";
    public const string DestinationNameAttribute = "messaging.destination.name";
    public const string OperationTypeAttribute = "messaging.operation.type";
    public const string OperationNameAttribute = "messaging.operation.name";
    public const string MessageIdAttribute = "messaging.message.id";
    public const string EventTypeAttribute = "event.type";
    public const string EventSequenceAttribute = "event.sequence";
    public const string ConnectionCountAttribute = "connection.count";
    public const string ConnectionIdAttribute = "connection.id";

    public const string MessagingSystem = "dapr";
    public const string ProcessOperation = "process";

    // Metric tag of events.dropped.
    public const string ReasonTag = "reason";

    public static string ProcessActivityName(string topic) => $"{ProcessOperation} {topic}";
}

// Why an event did not reach a browser; the closed set of events.dropped reasons.
public static class DropReasons
{
    public const string InvalidEnvelope = "invalid_envelope";
    public const string ConnectionClosed = "connection_closed";
    public const string ChannelFull = "channel_full";
}
