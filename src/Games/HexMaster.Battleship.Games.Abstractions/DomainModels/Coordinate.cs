namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// One cell of a board, zero-based.
public readonly record struct Coordinate(int Column, int Row);
