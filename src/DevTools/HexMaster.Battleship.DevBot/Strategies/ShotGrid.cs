using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Strategies;

internal enum CellState
{
    Unshot,
    Miss,
    Hit,
}

// The opponent's board as the bot knows it: every cell unshot, missed or hit.
internal sealed class ShotGrid
{
    private static readonly Coordinate[] Directions = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    private readonly CellState[,] cells = new CellState[Board.Size, Board.Size];

    public ShotGrid(IEnumerable<ShotView> shots)
    {
        foreach (var shot in shots)
        {
            var at = new Coordinate(shot.Column, shot.Row);
            if (Board.Contains(at))
            {
                cells[at.Column, at.Row] = shot.Hit ? CellState.Hit : CellState.Miss;
            }
        }
    }

    public static IEnumerable<Coordinate> AllCells()
    {
        for (var row = 0; row < Board.Size; row++)
        {
            for (var column = 0; column < Board.Size; column++)
            {
                yield return new Coordinate(column, row);
            }
        }
    }

    public static IEnumerable<Coordinate> Neighbours(Coordinate cell) =>
        Directions.Select(d => new Coordinate(cell.Column + d.Column, cell.Row + d.Row)).Where(Board.Contains);

    public CellState this[Coordinate cell] => cells[cell.Column, cell.Row];

    public bool IsUnshot(Coordinate cell) => Board.Contains(cell) && this[cell] == CellState.Unshot;

    public bool IsHit(Coordinate cell) => Board.Contains(cell) && this[cell] == CellState.Hit;

    public List<Coordinate> UnshotCells() => [.. AllCells().Where(IsUnshot)];

    public List<Coordinate> Hits() => [.. AllCells().Where(IsHit)];

    // Throws when there is nothing left to shoot at, so a strategy never returns a repeated cell.
    public List<Coordinate> RequireUnshotCells()
    {
        var unshot = UnshotCells();
        return unshot.Count > 0
            ? unshot
            : throw new InvalidOperationException("Every cell of the board has been shot at.");
    }
}
