using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexMaster.Battleship.Events.Abstractions;

// Who an integration event is delivered to. Travels as "player", "game" or "all" so every
// publisher produces the same JSON without configuring its serializer. There is no zero
// member on purpose: an envelope without a target kind binds to 0 and fails validation.
[JsonConverter(typeof(EventTargetJsonConverter))]
public enum EventTarget
{
    Player = 1,
    Game = 2,
    All = 3
}

public sealed class EventTargetJsonConverter()
    : JsonStringEnumConverter<EventTarget>(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
