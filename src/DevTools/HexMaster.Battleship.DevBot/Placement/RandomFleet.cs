using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Placement;

// A random, valid fleet: one ship of each kind, all on the board, none overlapping. Ships are
// placed longest first, each at a random position among those still free; a dead end restarts.
public static class RandomFleet
{
    public static IReadOnlyList<ShipPlacement> Create(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        while (true)
        {
            if (TryCreate(random) is { } fleet)
            {
                return fleet;
            }
        }
    }

    private static List<ShipPlacement>? TryCreate(Random random)
    {
        var fleet = new List<ShipPlacement>();
        var taken = new HashSet<Coordinate>();

        foreach (var kind in Fleet.Kinds.OrderByDescending(Fleet.LengthOf))
        {
            var options = Positions(kind).Where(p => p.Cells.All(c => Board.Contains(c) && !taken.Contains(c))).ToList();
            if (options.Count == 0)
            {
                return null;
            }

            var placement = options[random.Next(options.Count)];
            fleet.Add(placement);
            taken.UnionWith(placement.Cells);
        }

        return fleet;
    }

    private static IEnumerable<ShipPlacement> Positions(ShipKind kind)
    {
        foreach (var orientation in Enum.GetValues<Orientation>())
        {
            for (var row = 0; row < Board.Size; row++)
            {
                for (var column = 0; column < Board.Size; column++)
                {
                    yield return new ShipPlacement(kind, new Coordinate(column, row), orientation);
                }
            }
        }
    }
}
