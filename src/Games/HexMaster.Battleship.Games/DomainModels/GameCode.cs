using System.Diagnostics.CodeAnalysis;
using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Games.DomainModels;

// The code players share to find a game. It crosses the API, storage and the URL, so an invalid
// code cannot be represented: exactly eight characters, each A-Z or 0-9.
public readonly record struct GameCode
{
    public const int Length = 8;
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private GameCode(string value) => Value = value;

    public string Value => field ?? string.Empty;

    public static GameCode Parse(string? value) =>
        TryParse(value, out var code) ? code : throw new DomainException($"'{value}' is not a valid game code.");

    public static bool TryParse([NotNullWhen(true)] string? value, out GameCode code)
    {
        if (value is { Length: Length } && value.All(Alphabet.Contains))
        {
            code = new GameCode(value);
            return true;
        }

        code = default;
        return false;
    }

    public override string ToString() => Value;
}
