using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Games.Exceptions;

// Why a player cannot fire a shot.
public enum ShotRefusedReason
{
    // The game is not Started: nobody shoots before both fleets are ready or after the game ended.
    GameNotStarted,

    // The other player has the turn.
    NotYourTurn,

    // The player already fired at that cell.
    CellAlreadyShot,
}

// Thrown by Game.FireShot. Carries the reason so the endpoint maps it without matching on text.
public sealed class ShotRefusedException(string gameCode, ShotRefusedReason reason, string message)
    : DomainException(message)
{
    public string GameCode { get; } = gameCode;

    public ShotRefusedReason Reason { get; } = reason;
}
