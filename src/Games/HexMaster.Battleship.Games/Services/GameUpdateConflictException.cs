namespace HexMaster.Battleship.Games.Services;

// Every attempt to store a change to one game found it changed by another request. Nothing of this
// change was stored; the caller may try again.
public sealed class GameUpdateConflictException(string gameCode, int attempts)
    : Exception($"Game {gameCode} was changed by another request on each of {attempts} attempts.")
{
    public string GameCode { get; } = gameCode;
}
