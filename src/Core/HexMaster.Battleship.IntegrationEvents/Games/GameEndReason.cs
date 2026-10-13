using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexMaster.Battleship.IntegrationEvents.Games;

// Why a game ended. Travels as "completed", "abandoned-by-player" or "ended-by-server" so the
// payload is the same whatever serializer settings the publisher uses.
[JsonConverter(typeof(GameEndReasonJsonConverter))]
public enum GameEndReason
{
    Completed = 1,
    AbandonedByPlayer = 2,
    EndedByServer = 3
}

public sealed class GameEndReasonJsonConverter()
    : JsonStringEnumConverter<GameEndReason>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false);
