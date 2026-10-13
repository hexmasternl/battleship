using HexMaster.Battleship.Players.Configuration;

namespace HexMaster.Battleship.Players.Tests.Factories;

public static class TokenOptionsFactory
{
    // 32 ASCII characters, so exactly the minimum key size.
    public const string SigningKey = "battleship-test-signing-key-0032";

    public static TokenOptions Create(string signingKey = SigningKey) => new() { SigningKey = signingKey };
}
