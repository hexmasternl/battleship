using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Events.Connections;

// One open event stream of one player (one browser tab). Events are queued in a bounded
// channel that the stream endpoint drains; a slow client loses its oldest queued events
// rather than holding up the router.
public sealed class EventConnection
{
    private readonly Channel<SseItem<JsonElement>> _channel;

    public EventConnection(string playerId, int capacity, ILogger logger)
    {
        Id = Guid.NewGuid();
        PlayerId = playerId;

        _channel = Channel.CreateBounded<SseItem<JsonElement>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            },
            dropped => logger.LogWarning(
                "Event stream {ConnectionId} of player {PlayerId} is full; dropped event {EventId} of type {EventType}",
                Id, PlayerId, dropped.EventId, dropped.EventType));
    }

    public Guid Id { get; }

    public string PlayerId { get; }

    public ChannelReader<SseItem<JsonElement>> Reader => _channel.Reader;

    // False once the connection is completed; the caller then drops the connection.
    public bool TryWrite(SseItem<JsonElement> item) => _channel.Writer.TryWrite(item);

    public void Complete() => _channel.Writer.TryComplete();
}
