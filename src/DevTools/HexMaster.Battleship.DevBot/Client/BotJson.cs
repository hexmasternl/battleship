using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexMaster.Battleship.DevBot.Client;

// The wire format of the Games and Users APIs: web defaults, enums as camelCase strings.
internal static class BotJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
