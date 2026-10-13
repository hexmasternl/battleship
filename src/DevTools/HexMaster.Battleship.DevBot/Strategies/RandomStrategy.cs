using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Strategies;

// Any cell not shot at yet.
public sealed class RandomStrategy : IShotStrategy
{
    public Coordinate NextShot(IReadOnlyList<ShotView> myShots, Random random)
    {
        var unshot = new ShotGrid(myShots).RequireUnshotCells();
        return unshot[random.Next(unshot.Count)];
    }
}
