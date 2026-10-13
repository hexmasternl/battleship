using System.Net.ServerSentEvents;
using System.Text.Json;
using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Replay;
using HexMaster.Battleship.Events.Routing;
using HexMaster.Battleship.Events.Tests.Factories;
using HexMaster.Battleship.Events.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Tests.Routing;

public class EventRouterTests
{
    private readonly ConnectionRegistry _registry = new();
    private readonly ReplayBuffer _replayBuffer = ReplayBufferFactory.Create();
    private readonly EventRouter _router;

    public EventRouterTests()
    {
        _router = new EventRouter(
            _registry,
            _replayBuffer,
            Options.Create(new StreamOptions()),
            EventsMetricsFactory.Create(),
            NullLogger<EventRouter>.Instance);
    }

    [Fact]
    public void Connect_WithLastEventId_QueuesMissedEventsBeforeLiveOnes()
    {
        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));
        var missedOne = _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));
        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-b"));
        var missedTwo = _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        var connection = _router.Connect("user-a", missedOne.Sequence - 1);
        var live = _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        Assert.Equal(
            new[] { missedOne.Sequence, missedTwo.Sequence, live.Sequence }.Select(s => s.ToString()),
            Drain(connection).Select(i => i.EventId));
    }

    [Fact]
    public void Connect_WithoutLastEventId_ReceivesLiveEventsOnly()
    {
        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        var connection = _router.Connect("user-a", null);

        Assert.Empty(Drain(connection));
        Assert.Equal([connection], _registry.ConnectionsFor("user-a"));
    }

    [Fact]
    public void Disconnect_RemovesAndCompletesTheConnection()
    {
        var connection = _router.Connect("user-a", null);

        _router.Disconnect(connection);

        Assert.Empty(_registry.ConnectionsFor("user-a"));
        Assert.True(connection.Reader.Completion.IsCompleted);
    }

    [Fact]
    public void Route_AppendsEveryEventToTheReplayBuffer()
    {
        var routed = _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        Assert.Equal([routed.Sequence.ToString()], _replayBuffer.Since(0, "user-a").Select(i => i.EventId));
    }

    [Fact]
    public void Route_UserEvent_ReachesOnlyThatUser()
    {
        var a = Connect("user-a");
        var b = Connect("user-b");

        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        Assert.Single(Drain(a));
        Assert.Empty(Drain(b));
    }

    [Fact]
    public void Route_GameEvent_ReachesListedParticipantsOnly()
    {
        var a = Connect("user-a");
        var b = Connect("user-b");
        var c = Connect("user-c");

        _router.Route(IntegrationEventEnvelopeFactory.ForGame("user-a", "user-b"));

        Assert.Single(Drain(a));
        Assert.Single(Drain(b));
        Assert.Empty(Drain(c));
    }

    [Fact]
    public void Route_AllEvent_ReachesEveryone()
    {
        var a = Connect("user-a");
        var b = Connect("user-b");

        var result = _router.Route(IntegrationEventEnvelopeFactory.ForAll());

        Assert.Equal(2, result.DeliveredTo);
        Assert.Single(Drain(a));
        Assert.Single(Drain(b));
    }

    [Fact]
    public void Route_UserWithTwoConnections_ReceivesOnEach()
    {
        var first = Connect("user-a");
        var second = Connect("user-a");

        _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        Assert.Single(Drain(first));
        Assert.Single(Drain(second));
    }

    [Fact]
    public void Route_OneBrokenConnection_StillDeliversToOthersAndRemovesBrokenOne()
    {
        var broken = Connect("user-a");
        var healthy = Connect("user-b");
        broken.Complete();

        var result = _router.Route(IntegrationEventEnvelopeFactory.ForAll());

        Assert.Equal(1, result.DeliveredTo);
        Assert.Single(Drain(healthy));
        Assert.Empty(_registry.ConnectionsFor("user-a"));
    }

    [Fact]
    public void Route_DuplicateTargetIds_DeliversOncePerConnection()
    {
        var a = Connect("user-a");

        _router.Route(IntegrationEventEnvelopeFactory.ForGame("user-a", "user-a"));

        Assert.Single(Drain(a));
    }

    [Fact]
    public void Route_NoMatchingConnection_DeliversToNobody()
    {
        var result = _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a"));

        Assert.Equal(0, result.DeliveredTo);
    }

    [Fact]
    public void Route_AssignsIncreasingSequenceAsEventIdWithTypeAndPayload()
    {
        var a = Connect("user-a");
        var payload = JsonSerializer.SerializeToElement(new { x = 3, y = 7 });

        var first = _router.Route(IntegrationEventEnvelopeFactory.ForUser("user-a", "ships-placed"));
        var second = _router.Route(IntegrationEventEnvelopeFactory.Create(targetIds: ["user-a"], type: "shot-fired", payload: payload));

        Assert.Equal(first.Sequence + 1, second.Sequence);

        var items = Drain(a);
        Assert.Equal([first.Sequence.ToString(), second.Sequence.ToString()], items.Select(i => i.EventId));
        Assert.Equal("shot-fired", items[1].EventType);
        Assert.Equal(7, items[1].Data.GetProperty("y").GetInt32());
    }

    private EventConnection Connect(string userId)
    {
        var connection = EventConnectionFactory.Create(userId);
        _registry.Register(connection);
        return connection;
    }

    private static List<SseItem<JsonElement>> Drain(EventConnection connection)
    {
        var items = new List<SseItem<JsonElement>>();
        while (connection.Reader.TryRead(out var item))
        {
            items.Add(item);
        }

        return items;
    }
}
