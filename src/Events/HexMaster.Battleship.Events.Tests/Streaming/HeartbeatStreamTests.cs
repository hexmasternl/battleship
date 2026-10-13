using HexMaster.Battleship.Events.Streaming;
using HexMaster.Battleship.Events.Tests.Factories;
using Microsoft.Extensions.Time.Testing;

namespace HexMaster.Battleship.Events.Tests.Streaming;

public class HeartbeatStreamTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task ReadAsync_IdleForTheInterval_YieldsAHeartbeat()
    {
        var connection = EventConnectionFactory.Create();
        await using var stream = HeartbeatStream.ReadAsync(connection.Reader, Interval, _time).GetAsyncEnumerator();

        var next = stream.MoveNextAsync().AsTask();
        _time.Advance(Interval - TimeSpan.FromSeconds(1));
        Assert.False(next.IsCompleted);

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.True(await next.WaitAsync(Timeout));
        Assert.Equal(SseItems.HeartbeatEventType, stream.Current.EventType);
        Assert.Null(stream.Current.EventId);
    }

    [Fact]
    public async Task ReadAsync_WhileEventsFlow_YieldsNoHeartbeat()
    {
        var connection = EventConnectionFactory.Create();
        await using var stream = HeartbeatStream.ReadAsync(connection.Reader, Interval, _time).GetAsyncEnumerator();

        for (var sequence = 1; sequence <= 4; sequence++)
        {
            var next = stream.MoveNextAsync().AsTask();
            _time.Advance(TimeSpan.FromSeconds(10));
            connection.TryWrite(SseItems.FromEnvelope(sequence, IntegrationEventEnvelopeFactory.Create()));

            Assert.True(await next.WaitAsync(Timeout));
            Assert.Equal(sequence.ToString(), stream.Current.EventId);
        }
    }

    [Fact]
    public async Task ReadAsync_CompletedConnection_EndsTheStream()
    {
        var connection = EventConnectionFactory.Create();
        connection.TryWrite(SseItems.FromEnvelope(1, IntegrationEventEnvelopeFactory.Create()));
        connection.Complete();

        var items = new List<string?>();
        await foreach (var item in HeartbeatStream.ReadAsync(connection.Reader, Interval, _time).WithCancellation(new CancellationTokenSource(Timeout).Token))
        {
            items.Add(item.EventId);
        }

        Assert.Equal(["1"], items);
    }

    [Fact]
    public async Task ReadAsync_Canceled_EndsTheStream()
    {
        var connection = EventConnectionFactory.Create();
        using var cancellation = new CancellationTokenSource();
        await using var stream = HeartbeatStream.ReadAsync(connection.Reader, Interval, _time, cancellation.Token).GetAsyncEnumerator();

        var next = stream.MoveNextAsync().AsTask();
        await cancellation.CancelAsync();

        Assert.False(await next.WaitAsync(Timeout));
    }
}
