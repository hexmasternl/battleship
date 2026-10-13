using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace HexMaster.Battleship.Events.Streaming;

public static class HeartbeatStream
{
    // Yields the connection's events and, whenever none arrived for a full interval, a
    // heartbeat. Every event restarts the interval. The heartbeat write keeps proxies from
    // closing an idle stream and doubles as the probe that detects a dead socket: the write
    // fails and the endpoint drops the connection. Ends when the channel completes.
    public static async IAsyncEnumerable<SseItem<JsonElement>> ReadAsync(
        ChannelReader<SseItem<JsonElement>> reader,
        TimeSpan interval,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Task<bool>? waitForEvent = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            while (reader.TryRead(out var item))
            {
                yield return item;
            }

            waitForEvent ??= reader.WaitToReadAsync(cancellationToken).AsTask();

            using var stopDelay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = Task.Delay(interval, timeProvider, stopDelay.Token);

            var first = await Task.WhenAny(waitForEvent, delay);
            await stopDelay.CancelAsync();

            if (first == delay)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return SseItems.Heartbeat;
                continue;
            }

            // Canceled or faulted waits end the stream like a completed channel.
            if (!waitForEvent.IsCompletedSuccessfully || !waitForEvent.Result)
            {
                yield break;
            }

            waitForEvent = null;
        }
    }
}
