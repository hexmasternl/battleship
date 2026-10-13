namespace HexMaster.Battleship.IntegrationEvents.Tests.Integration;

public sealed record SseMessage(string? Id, string? Event, string Data);

// Minimal text/event-stream parser for tests: reads one message per call (fields up to the
// blank line that terminates it). Comment lines are skipped.
public sealed class SseStreamReader(Stream stream) : IDisposable
{
    private readonly StreamReader _reader = new(stream);

    public async Task<SseMessage> ReadMessageAsync(CancellationToken cancellationToken)
    {
        string? id = null;
        string? eventType = null;
        var data = new List<string>();

        while (true)
        {
            var line = await _reader.ReadLineAsync(cancellationToken).AsTask().WaitAsync(cancellationToken)
                ?? throw new EndOfStreamException("The event stream ended.");

            if (line.Length == 0)
            {
                if (id is null && eventType is null && data.Count == 0)
                {
                    continue;
                }

                return new SseMessage(id, eventType, string.Join('\n', data));
            }

            if (line.StartsWith(':'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            switch (field)
            {
                case "id":
                    id = value;
                    break;
                case "event":
                    eventType = value;
                    break;
                case "data":
                    data.Add(value);
                    break;
            }
        }
    }

    public void Dispose() => _reader.Dispose();
}
