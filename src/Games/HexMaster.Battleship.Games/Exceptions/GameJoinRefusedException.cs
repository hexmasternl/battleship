using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Games.Exceptions;

// Why a player who does not play in a game cannot join it.
public enum GameJoinRefusedReason
{
    // The game is no longer New: started, completed or abandoned.
    NotJoinable,

    // The game is New, but another player already took the second seat.
    GameFull,
}

// Thrown by Game.Join. Carries the reason so the endpoint maps it without matching on text.
public sealed class GameJoinRefusedException(string gameCode, GameJoinRefusedReason reason)
    : DomainException(reason == GameJoinRefusedReason.GameFull
        ? $"Game {gameCode} already has two players."
        : $"Game {gameCode} can no longer be joined.")
{
    public string GameCode { get; } = gameCode;

    public GameJoinRefusedReason Reason { get; } = reason;
}
