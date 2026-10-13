using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Strategies;

// Counts, for every ship of the fleet, each horizontal and vertical placement that covers no miss,
// and fires at the unshot cell covered by the most of them. A placement through a hit counts
// HitWeight times, which steers the bot to finish a ship it found. Sunk ships are unknown, so the
// whole fleet is always counted. Ties are broken at random.
public sealed class ProbabilityDensityStrategy : IShotStrategy
{
    internal const int HitWeight = 20;

    public Coordinate NextShot(IReadOnlyList<ShotView> myShots, Random random)
    {
        var grid = new ShotGrid(myShots);
        var unshot = grid.RequireUnshotCells();
        var scores = Scores(grid);

        var best = unshot.Max(cell => scores[cell.Column, cell.Row]);
        var candidates = unshot.Where(cell => scores[cell.Column, cell.Row] == best).ToList();
        return candidates[random.Next(candidates.Count)];
    }

    internal static int[,] Scores(ShotGrid grid)
    {
        var scores = new int[Board.Size, Board.Size];

        foreach (var kind in Fleet.Kinds)
        {
            var length = Fleet.LengthOf(kind);
            foreach (var (dc, dr) in new[] { (1, 0), (0, 1) })
            {
                for (var row = 0; row + dr * (length - 1) < Board.Size; row++)
                {
                    for (var column = 0; column + dc * (length - 1) < Board.Size; column++)
                    {
                        AddPlacement(grid, scores, column, row, dc, dr, length);
                    }
                }
            }
        }

        return scores;
    }

    private static void AddPlacement(ShotGrid grid, int[,] scores, int column, int row, int dc, int dr, int length)
    {
        var coversHit = false;
        for (var i = 0; i < length; i++)
        {
            switch (grid[new Coordinate(column + dc * i, row + dr * i)])
            {
                case CellState.Miss:
                    return;
                case CellState.Hit:
                    coversHit = true;
                    break;
            }
        }

        var weight = coversHit ? HitWeight : 1;
        for (var i = 0; i < length; i++)
        {
            var cell = new Coordinate(column + dc * i, row + dr * i);
            if (grid[cell] == CellState.Unshot)
            {
                scores[cell.Column, cell.Row] += weight;
            }
        }
    }
}
