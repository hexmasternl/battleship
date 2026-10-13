using System.Text.Json;
using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.IntegrationEvents;

namespace HexMaster.Battleship.DevBot.Tests.Factories;

internal static class EnvelopeFactory
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static IntegrationEventEnvelope Create<TEvent>(TEvent payload, DateTimeOffset occurredAt, params Guid[] targets)
        where TEvent : IIntegrationEvent =>
        new(
            Guid.NewGuid().ToString(),
            TEvent.EventType,
            targets.Length == 0 ? EventTarget.All : EventTarget.Game,
            [.. targets.Select(t => t.ToString())],
            occurredAt,
            JsonSerializer.SerializeToElement(payload, Web));
}
