using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.IntegrationEvents.Observability;

// Log messages of the integration event sender; event ids 3000-3999. The payload is never logged.
internal static partial class IntegrationEventsLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Published integration event {MessageId} of type {EventType} to {Topic}")]
    public static partial void Published(ILogger logger, string messageId, string eventType, string topic);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Publishing integration event {MessageId} of type {EventType} to {Topic} failed")]
    public static partial void PublishFailed(ILogger logger, Exception exception, string messageId, string eventType, string topic);
}
