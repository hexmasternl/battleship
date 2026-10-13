using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Strategies;

// Hunts at random until it hits, then targets. The API never says a ship is sunk, so every hit
// with an unshot neighbour counts as unresolved. Two or more hits in a line are extended at
// either end first; otherwise any unshot neighbour of a hit is next.
public class HuntAndTargetStrategy : IShotStrategy
{
    public Coordinate NextShot(IReadOnlyList<ShotView> myShots, Random random)
    {
        var grid = new ShotGrid(myShots);
        var unshot = grid.RequireUnshotCells();

        var target = TargetCells(grid);
        if (target.Count > 0)
        {
            return target[random.Next(target.Count)];
        }

        var hunt = HuntCells(unshot);
        return hunt[random.Next(hunt.Count)];
    }

    // The cells to hunt on when nothing is left to target. Never empty: unshot holds at least one cell.
    protected virtual IReadOnlyList<Coordinate> HuntCells(IReadOnlyList<Coordinate> unshot) => unshot;

    internal static IReadOnlyList<Coordinate> TargetCells(ShotGrid grid)
    {
        var hits = grid.Hits();

        var lineEnds = new HashSet<Coordinate>();
        foreach (var hit in hits)
        {
            foreach (var (dc, dr) in new[] { (1, 0), (0, 1) })
            {
                // Only start at the first hit of a line, so each line is walked once per axis.
                if (grid.IsHit(new Coordinate(hit.Column - dc, hit.Row - dr)))
                {
                    continue;
                }

                var length = 1;
                while (grid.IsHit(new Coordinate(hit.Column + dc * length, hit.Row + dr * length)))
                {
                    length++;
                }

                if (length < 2)
                {
                    continue;
                }

                var before = new Coordinate(hit.Column - dc, hit.Row - dr);
                var after = new Coordinate(hit.Column + dc * length, hit.Row + dr * length);
                if (grid.IsUnshot(before))
                {
                    lineEnds.Add(before);
                }

                if (grid.IsUnshot(after))
                {
                    lineEnds.Add(after);
                }
            }
        }

        if (lineEnds.Count > 0)
        {
            return [.. lineEnds];
        }

        return [.. hits.SelectMany(ShotGrid.Neighbours).Where(grid.IsUnshot).Distinct()];
    }
}
