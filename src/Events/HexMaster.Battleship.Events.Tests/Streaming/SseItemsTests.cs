using System.Text.Json;
using HexMaster.Battleship.Events.Streaming;
using HexMaster.Battleship.Events.Tests.Factories;

namespace HexMaster.Battleship.Events.Tests.Streaming;

public class SseItemsTests
{
    [Fact]
    public void FromEnvelope_MapsSequenceTypeAndPayload()
    {
        var payload = JsonSerializer.SerializeToElement(new { row = 4 });
        var envelope = IntegrationEventEnvelopeFactory.Create(type: "shot-fired", payload: payload);

        var item = SseItems.FromEnvelope(42, envelope);

        Assert.Equal("42", item.EventId);
        Assert.Equal("shot-fired", item.EventType);
        Assert.Equal(4, item.Data.GetProperty("row").GetInt32());
    }

    [Fact]
    public void FromEnvelope_MissingPayload_BecomesJsonNull()
    {
        var envelope = IntegrationEventEnvelopeFactory.Create() with { Payload = default };

        Assert.Equal(JsonValueKind.Null, SseItems.FromEnvelope(1, envelope).Data.ValueKind);
    }

    [Fact]
    public void Heartbeat_HasTypeAndNoId()
    {
        Assert.Equal("heartbeat", SseItems.Heartbeat.EventType);
        Assert.Null(SseItems.Heartbeat.EventId);
    }
}
