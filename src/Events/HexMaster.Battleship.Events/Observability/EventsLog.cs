using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Events.Observability;

// Log messages of the Events service; event ids 2000-2999. Payloads are never logged.
public static partial class EventsLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Opened event stream {ConnectionId} for player {PlayerId} after event {LastEventId}, replaying {ReplayCount} events")]
    public static partial void StreamOpened(ILogger logger, Guid connectionId, string playerId, long? lastEventId, int replayCount);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Closed event stream {ConnectionId} of player {PlayerId}")]
    public static partial void StreamClosed(ILogger logger, Guid connectionId, string playerId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Removing closed event stream {ConnectionId} of player {PlayerId}")]
    public static partial void ClosedStreamRemoved(ILogger logger, Guid connectionId, string playerId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Event stream {ConnectionId} of player {PlayerId} is full; dropped event {EventId} of type {EventType}")]
    public static partial void ChannelFullDropped(ILogger logger, Guid connectionId, string playerId, string? eventId, string eventType);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Dropped an integration event that is not a valid envelope")]
    public static partial void UnreadableEnvelopeDropped(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning, Message = "Dropped invalid integration event {EventId} of type {EventType}: {Errors}")]
    public static partial void InvalidEnvelopeDropped(ILogger logger, string? eventId, string? eventType, string errors);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Routed integration event {EventId} of type {EventType} as {Sequence} to {ConnectionCount} connections")]
    public static partial void EventRouted(ILogger logger, string eventId, string eventType, long sequence, int connectionCount);
}
