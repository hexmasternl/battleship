using HexMaster.Battleship.DevBot.Placement;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Placement;

public sealed class RandomFleetTests
{
    [Fact]
    public void Create_ThousandSeeds_AllFleetsAreValid()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var fleet = RandomFleet.Create(new Random(seed));
            var cells = fleet.SelectMany(s => s.Cells).ToList();

            Assert.Equal(Fleet.Kinds.Order(), fleet.Select(s => s.Kind).Order());
            Assert.All(cells, c => Assert.True(Board.Contains(c), $"Seed {seed} placed {c} off the board."));
            Assert.Equal(cells.Count, cells.Distinct().Count());
            Assert.Equal(17, cells.Count);
        }
    }

    [Fact]
    public void Create_DifferentSeeds_GiveDifferentFleets()
    {
        var first = RandomFleet.Create(new Random(1));
        var second = RandomFleet.Create(new Random(2));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_SameSeed_GivesTheSameFleet()
    {
        Assert.Equal(RandomFleet.Create(new Random(7)), RandomFleet.Create(new Random(7)));
    }
}
