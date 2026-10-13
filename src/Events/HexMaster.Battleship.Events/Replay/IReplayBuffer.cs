using System.Net.ServerSentEvents;
using System.Text.Json;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.Events.Replay;

public interface IReplayBuffer
{
    void Append(long sequence, IntegrationEventEnvelope envelope, SseItem<JsonElement> item);

    // The buffered events after lastEventId that target the player, in order. Empty when
    // lastEventId has already fallen out of the buffer: the client then continues live only.
    IReadOnlyList<SseItem<JsonElement>> Since(long lastEventId, string playerId);
}
