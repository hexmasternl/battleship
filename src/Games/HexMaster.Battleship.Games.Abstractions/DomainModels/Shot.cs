namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// A shot a player fired at the opponent's board, and whether it hit a ship.
public readonly record struct Shot(int Column, int Row, bool Hit);
