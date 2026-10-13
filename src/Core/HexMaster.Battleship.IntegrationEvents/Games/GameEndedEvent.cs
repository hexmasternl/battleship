using System.Text.Json.Serialization;

namespace HexMaster.Battleship.IntegrationEvents.Games;

// WinnerId is left out of the payload when the game ended without a winner, for example when
// the server ended an inactive game.
public sealed record GameEndedEvent(
    Guid GameId,
    string GameCode,
    GameEndReason Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? WinnerId = null) : IIntegrationEvent
{
    public static string EventType => "game.ended.v1";
}
