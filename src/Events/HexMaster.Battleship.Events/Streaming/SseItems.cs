using System.Globalization;
using System.Net.ServerSentEvents;
using System.Text.Json;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.Events.Streaming;

// Maps routed events onto server-sent events messages: id is the per-process sequence
// (Last-Event-ID replay needs an ordering), event is the envelope type, data the payload.
public static class SseItems
{
    public const string HeartbeatEventType = "heartbeat";

    private static readonly JsonElement JsonNull = JsonDocument.Parse("null").RootElement.Clone();

    // Carries no id, so a heartbeat never moves the client's last event id.
    public static SseItem<JsonElement> Heartbeat { get; } = new(JsonNull, HeartbeatEventType);

    public static SseItem<JsonElement> FromEnvelope(long sequence, IntegrationEventEnvelope envelope) =>
        new(envelope.Payload.ValueKind == JsonValueKind.Undefined ? JsonNull : envelope.Payload, envelope.Type)
        {
            EventId = sequence.ToString(CultureInfo.InvariantCulture)
        };
}
