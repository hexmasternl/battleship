using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;
using HexMaster.Battleship.Events.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Events.Connections;

// One open event stream of one player (one browser tab). Events are queued in a bounded
// channel that the stream endpoint drains; a slow client loses its oldest queued events
// rather than holding up the router.
public sealed class EventConnection
{
    private readonly Channel<SseItem<JsonElement>> _channel;
    private int _closed;

    public EventConnection(string playerId, int capacity, ILogger logger, EventsMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        Id = Guid.NewGuid();
        PlayerId = playerId;

        _channel = Channel.CreateBounded<SseItem<JsonElement>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            },
            dropped =>
            {
                metrics.Dropped(DropReasons.ChannelFull);
                EventsLog.ChannelFullDropped(logger, Id, PlayerId, dropped.EventId, dropped.EventType);
            });
    }

    public Guid Id { get; }

    public string PlayerId { get; }

    public ChannelReader<SseItem<JsonElement>> Reader => _channel.Reader;

    // False once the connection is completed; the caller then drops the connection.
    public bool TryWrite(SseItem<JsonElement> item) => _channel.Writer.TryWrite(item);

    public void Complete() => _channel.Writer.TryComplete();

    // True only the first time, so a connection leaves the active-connections gauge once.
    internal bool TryMarkClosed() => Interlocked.Exchange(ref _closed, 1) == 0;
}
