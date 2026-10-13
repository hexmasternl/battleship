using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Replay;
using HexMaster.Battleship.Events.Streaming;
using HexMaster.Battleship.Events.Tests.Factories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace HexMaster.Battleship.Events.Tests.Replay;

public class ReplayBufferTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void Since_ReturnsMissedEventsForTheUserInOrder()
    {
        var buffer = ReplayBufferFactory.Create(timeProvider: _time);
        for (var sequence = 1; sequence <= 12; sequence++)
        {
            Append(buffer, sequence, IntegrationEventEnvelopeFactory.ForUser("user-a"));
        }

        Assert.Equal(["11", "12"], buffer.Since(10, "user-a").Select(i => i.EventId));
    }

    [Fact]
    public void Since_DoesNotLeakOtherUsersEvents()
    {
        var buffer = ReplayBufferFactory.Create(timeProvider: _time);
        Append(buffer, 1, IntegrationEventEnvelopeFactory.ForUser("user-a"));
        Append(buffer, 2, IntegrationEventEnvelopeFactory.ForUser("user-b"));
        Append(buffer, 3, IntegrationEventEnvelopeFactory.ForGame("user-b", "user-c"));

        Assert.Empty(buffer.Since(1, "user-a"));
    }

    [Fact]
    public void Since_IncludesGameAndBroadcastEventsForTheUser()
    {
        var buffer = ReplayBufferFactory.Create(timeProvider: _time);
        Append(buffer, 1, IntegrationEventEnvelopeFactory.ForGame("user-a", "user-b"));
        Append(buffer, 2, IntegrationEventEnvelopeFactory.ForAll());
        Append(buffer, 3, IntegrationEventEnvelopeFactory.ForUser("user-c"));

        Assert.Equal(["1", "2"], buffer.Since(0, "user-a").Select(i => i.EventId));
    }

    [Fact]
    public void Since_IdOlderThanTheBuffer_FallsThroughToLiveOnly()
    {
        var buffer = ReplayBufferFactory.Create(capacity: 3, timeProvider: _time);
        for (var sequence = 1; sequence <= 5; sequence++)
        {
            Append(buffer, sequence, IntegrationEventEnvelopeFactory.ForUser("user-a"));
        }

        Assert.Empty(buffer.Since(1, "user-a"));
    }

    [Fact]
    public void Append_BeyondCapacity_DropsTheOldestEntries()
    {
        var buffer = ReplayBufferFactory.Create(capacity: 3, timeProvider: _time);
        for (var sequence = 1; sequence <= 5; sequence++)
        {
            Append(buffer, sequence, IntegrationEventEnvelopeFactory.ForUser("user-a"));
        }

        Assert.Equal(["3", "4", "5"], buffer.Since(2, "user-a").Select(i => i.EventId));
    }

    [Fact]
    public void Since_EntriesOlderThanMaxAge_AreNotReplayed()
    {
        var buffer = ReplayBufferFactory.Create(maxAge: TimeSpan.FromMinutes(2), timeProvider: _time);
        Append(buffer, 1, IntegrationEventEnvelopeFactory.ForUser("user-a"));
        _time.Advance(TimeSpan.FromMinutes(3));
        Append(buffer, 2, IntegrationEventEnvelopeFactory.ForUser("user-a"));

        Assert.Empty(buffer.Since(0, "user-a"));
        Assert.Equal(["2"], buffer.Since(1, "user-a").Select(i => i.EventId));
    }

    [Fact]
    public void Since_EmptyBuffer_ReturnsNothing()
    {
        Assert.Empty(ReplayBufferFactory.Create(timeProvider: _time).Since(0, "user-a"));
    }

    [Fact]
    public void AddEventsCore_BindsReplayOptionsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Events:Replay:Capacity"] = "10",
                ["Events:Replay:MaxAge"] = "00:00:30"
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddEventsCore()
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ReplayOptions>>().Value;
        Assert.Equal(10, options.Capacity);
        Assert.Equal(TimeSpan.FromSeconds(30), options.MaxAge);
        Assert.IsType<ReplayBuffer>(provider.GetRequiredService<IReplayBuffer>());
    }

    [Fact]
    public void ReplayOptions_HaveTheDesignDefaults()
    {
        var options = new ReplayOptions();

        Assert.Equal(1000, options.Capacity);
        Assert.Equal(TimeSpan.FromMinutes(2), options.MaxAge);
    }

    private static void Append(ReplayBuffer buffer, long sequence, IntegrationEventEnvelope envelope) =>
        buffer.Append(sequence, envelope, SseItems.FromEnvelope(sequence, envelope));
}
