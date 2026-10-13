using System.Buffers.Text;
using System.Text;
using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.DevBot.Tests.Factories;

internal static class TokenFactory
{
    // An unsigned JWT naming the user; the bot reads it without validating.
    public static string Jwt(Guid userId, string id = "")
    {
        static string Encode(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

        return $"{Encode("""{"alg":"HS256","typ":"JWT"}""")}.{Encode($$"""{"sub":"{{userId}}","name":"Dev Bot","jti":"{{id}}"}""")}.signature";
    }

    public static TokenPairResponse Pair(Guid userId, DateTimeOffset now, string id = "1") =>
        new(Jwt(userId, id), now.AddMinutes(15), $"refresh-{id}", now.AddDays(30));
}
