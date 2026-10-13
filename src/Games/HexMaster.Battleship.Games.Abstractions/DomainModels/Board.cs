namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// The square board both players use. Columns and rows are zero-based, 0 up to Size - 1.
public static class Board
{
    public const int Size = 12;

    public static bool Contains(Coordinate coordinate) =>
        coordinate.Column is >= 0 and < Size && coordinate.Row is >= 0 and < Size;
}
