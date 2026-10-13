using System.Net.ServerSentEvents;
using System.Text.Json;
using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Replay;

// Ring of the most recently routed events, capped by count and by age. Every routed event is
// appended, delivered or not, so the buffered sequence numbers are contiguous.
public sealed class ReplayBuffer(IOptions<ReplayOptions> options, TimeProvider timeProvider) : IReplayBuffer
{
    private readonly Queue<Entry> _entries = new();
    private readonly Lock _lock = new();

    public void Append(long sequence, IntegrationEventEnvelope envelope, SseItem<JsonElement> item)
    {
        var entry = new Entry(
            sequence,
            timeProvider.GetUtcNow(),
            envelope.Target == EventTarget.All ? null : envelope.TargetIds.ToHashSet(StringComparer.Ordinal),
            item);

        lock (_lock)
        {
            _entries.Enqueue(entry);

            while (_entries.Count > Math.Max(options.Value.Capacity, 0))
            {
                _entries.Dequeue();
            }

            PruneExpired();
        }
    }

    public IReadOnlyList<SseItem<JsonElement>> Since(long lastEventId, string playerId)
    {
        lock (_lock)
        {
            PruneExpired();

            // Events between lastEventId and the oldest entry are gone; a partial replay
            // would hide that gap, so the client gets live events only.
            if (_entries.Count == 0 || _entries.Peek().Sequence > lastEventId + 1)
            {
                return [];
            }

            return _entries
                .Where(e => e.Sequence > lastEventId && (e.TargetIds is null || e.TargetIds.Contains(playerId)))
                .Select(e => e.Item)
                .ToList();
        }
    }

    private void PruneExpired()
    {
        var cutoff = timeProvider.GetUtcNow() - options.Value.MaxAge;

        while (_entries.Count > 0 && _entries.Peek().RoutedAt < cutoff)
        {
            _entries.Dequeue();
        }
    }

    // TargetIds is null for an event that targets everyone.
    private sealed record Entry(long Sequence, DateTimeOffset RoutedAt, HashSet<string>? TargetIds, SseItem<JsonElement> Item);
}
