using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Strategies;

// Hunt and target, but hunting only on the checkerboard cells where column plus row is even.
// The shortest ship covers two cells, so every ship covers at least one of them. Falls back to any
// unshot cell once the checkerboard is used up.
public sealed class HuntWithParityStrategy : HuntAndTargetStrategy
{
    public static bool IsParityCell(Coordinate cell) => (cell.Column + cell.Row) % 2 == 0;

    protected override IReadOnlyList<Coordinate> HuntCells(IReadOnlyList<Coordinate> unshot)
    {
        var parity = unshot.Where(IsParityCell).ToList();
        return parity.Count > 0 ? parity : unshot;
    }
}
