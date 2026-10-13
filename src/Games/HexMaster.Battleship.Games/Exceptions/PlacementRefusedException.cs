using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Games.Exceptions;

// Why a player cannot place a ship, or cannot become ready.
public enum PlacementRefusedReason
{
    // The game is no longer New: placement is over.
    GameAlreadyStarted,

    // The player is ready, so the fleet is fixed.
    PlayerReady,

    // A cell of the ship lies outside the board.
    OutOfBoard,

    // A cell of the ship is taken by another ship of the same player.
    Overlap,

    // The player wants to be ready but has not placed every ship.
    FleetIncomplete,
}

// Thrown by Game.PlaceShip and Game.MarkReady. Carries the reason so the endpoint maps it without
// matching on text.
public sealed class PlacementRefusedException(string gameCode, PlacementRefusedReason reason, string message)
    : DomainException(message)
{
    public string GameCode { get; } = gameCode;

    public PlacementRefusedReason Reason { get; } = reason;
}
