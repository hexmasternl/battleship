using System.Security.Cryptography;
using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Services;

// Draws each character uniformly from the 36-symbol alphabet with a cryptographic generator,
// so a code cannot be guessed from earlier ones.
public sealed class GameCodeGenerator : IGameCodeGenerator
{
    public GameCode Generate() =>
        GameCode.Parse(new string(RandomNumberGenerator.GetItems<char>(GameCode.Alphabet, GameCode.Length)));
}
