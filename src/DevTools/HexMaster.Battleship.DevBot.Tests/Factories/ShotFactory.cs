using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Factories;

internal static class ShotFactory
{
    public static ShotView Miss(int column, int row) => new(column, row, false);

    public static ShotView Hit(int column, int row) => new(column, row, true);

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

    // Misses on every cell except the given ones.
    public static List<ShotView> MissesEverywhereExcept(params Coordinate[] free) =>
        [.. AllCells().Where(c => !free.Contains(c)).Select(c => Miss(c.Column, c.Row))];
}
