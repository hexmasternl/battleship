namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// The answer to a recorded shot: the cell fired at and whether it hit a ship.
public sealed record FireShotResponse(int Column, int Row, bool Hit);
