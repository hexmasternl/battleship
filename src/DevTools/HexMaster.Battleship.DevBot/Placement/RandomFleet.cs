using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Placement;

// A random, valid fleet: one ship of each kind, all on the board, none overlapping. Ships are
// placed longest first, each at a random position among those still free; a dead end restarts.
public static class RandomFleet
{
    public static IReadOnlyList<ShipPlacement> Create(Random random) => Complete(random, []);

    // The ships still missing next to the ones already placed, for a game resumed half-way through
    // placement. Returns only the new placements. Gives up after 100 dead ends, which only happens
    // when the placed ships leave no room.
    public static IReadOnlyList<ShipPlacement> Complete(Random random, IReadOnlyCollection<ShipPlacement> placed)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(placed);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (TryCreate(random, placed) is { } fleet)
            {
                return fleet;
            }
        }

        throw new InvalidOperationException("The placed ships leave no room for the rest of the fleet.");
    }

    private static List<ShipPlacement>? TryCreate(Random random, IReadOnlyCollection<ShipPlacement> placed)
    {
        var fleet = new List<ShipPlacement>();
        var taken = placed.SelectMany(p => p.Cells).ToHashSet();
        var placedKinds = placed.Select(p => p.Kind).ToHashSet();

        foreach (var kind in Fleet.Kinds.Where(k => !placedKinds.Contains(k)).OrderByDescending(Fleet.LengthOf))
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
