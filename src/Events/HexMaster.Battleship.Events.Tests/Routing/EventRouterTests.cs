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
        _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));
        var missedOne = _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));
        _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-b"));
        var missedTwo = _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));

        var connection = _router.Connect("player-a", missedOne.Sequence - 1);
        var live = _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));

        Assert.Equal(
            new[] { missedOne.Sequence, missedTwo.Sequence, live.Sequence }.Select(s => s.ToString()),
            Drain(connection).Select(i => i.EventId));
    }

    [Fact]
    public void Connect_WithoutLastEventId_ReceivesLiveEventsOnly()
    {
        _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));

        var connection = _router.Connect("player-a", null);

        Assert.Empty(Drain(connection));
        Assert.Equal([connection], _registry.ConnectionsFor("player-a"));
    }

    [Fact]
    public void Disconnect_RemovesAndCompletesTheConnection()
    {
        var connection = _router.Connect("player-a", null);

        _router.Disconnect(connection);

        Assert.Empty(_registry.ConnectionsFor("player-a"));
        Assert.True(connection.Reader.Completion.IsCompleted);
    }

    [Fact]
    public void Route_AppendsEveryEventToTheReplayBuffer()
    {
        var routed = _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));

        Assert.Equal([routed.Sequence.ToString()], _replayBuffer.Since(0, "player-a").Select(i => i.EventId));
    }

    [Fact]
    public void Route_PlayerEvent_ReachesOnlyThatPlayer()
    {
        var a = Connect("player-a");
        var b = Connect("player-b");

        _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));

        Assert.Single(Drain(a));
        Assert.Empty(Drain(b));
    }

    [Fact]
    public void Route_GameEvent_ReachesListedParticipantsOnly()
    {
        var a = Connect("player-a");
        var b = Connect("player-b");
        var c = Connect("player-c");

        _router.Route(IntegrationEventEnvelopeFactory.ForGame("player-a", "player-b"));

        Assert.Single(Drain(a));
        Assert.Single(Drain(b));
        Assert.Empty(Drain(c));
    }

    [Fact]
    public void Route_AllEvent_ReachesEveryone()
    {
        var a = Connect("player-a");
        var b = Connect("player-b");

        var result = _router.Route(IntegrationEventEnvelopeFactory.ForAll());

        Assert.Equal(2, result.DeliveredTo);
        Assert.Single(Drain(a));
        Assert.Single(Drain(b));
    }

    [Fact]
    public void Route_PlayerWithTwoConnections_ReceivesOnEach()
    {
        var first = Connect("player-a");
        var second = Connect("player-a");

        _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));

        Assert.Single(Drain(first));
        Assert.Single(Drain(second));
    }

    [Fact]
    public void Route_OneBrokenConnection_StillDeliversToOthersAndRemovesBrokenOne()
    {
        var broken = Connect("player-a");
        var healthy = Connect("player-b");
        broken.Complete();

        var result = _router.Route(IntegrationEventEnvelopeFactory.ForAll());

        Assert.Equal(1, result.DeliveredTo);
        Assert.Single(Drain(healthy));
        Assert.Empty(_registry.ConnectionsFor("player-a"));
    }

    [Fact]
    public void Route_DuplicateTargetIds_DeliversOncePerConnection()
    {
        var a = Connect("player-a");

        _router.Route(IntegrationEventEnvelopeFactory.ForGame("player-a", "player-a"));

        Assert.Single(Drain(a));
    }

    [Fact]
    public void Route_NoMatchingConnection_DeliversToNobody()
    {
        var result = _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a"));

        Assert.Equal(0, result.DeliveredTo);
    }

    [Fact]
    public void Route_AssignsIncreasingSequenceAsEventIdWithTypeAndPayload()
    {
        var a = Connect("player-a");
        var payload = JsonSerializer.SerializeToElement(new { x = 3, y = 7 });

        var first = _router.Route(IntegrationEventEnvelopeFactory.ForPlayer("player-a", "ships-placed"));
        var second = _router.Route(IntegrationEventEnvelopeFactory.Create(targetIds: ["player-a"], type: "shot-fired", payload: payload));

        Assert.Equal(first.Sequence + 1, second.Sequence);

        var items = Drain(a);
        Assert.Equal([first.Sequence.ToString(), second.Sequence.ToString()], items.Select(i => i.EventId));
        Assert.Equal("shot-fired", items[1].EventType);
        Assert.Equal(7, items[1].Data.GetProperty("y").GetInt32());
    }

    private EventConnection Connect(string playerId)
    {
        var connection = EventConnectionFactory.Create(playerId);
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
