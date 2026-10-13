using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Strategies;

public sealed class HuntAndTargetStrategyTests
{
    private readonly HuntAndTargetStrategy strategy = new();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    public void NextShot_SingleHit_PicksANeighbour(int seed)
    {
        List<ShotView> shots = [ShotFactory.Miss(0, 0), ShotFactory.Hit(5, 5)];

        var next = strategy.NextShot(shots, new Random(seed));

        Assert.Contains(next, new Coordinate[] { new(4, 5), new(6, 5), new(5, 4), new(5, 6) });
    }

    [Fact]
    public void NextShot_SingleHitWithOneShotNeighbour_PicksAnUnshotNeighbour()
    {
        List<ShotView> shots = [ShotFactory.Hit(5, 5), ShotFactory.Miss(6, 5), ShotFactory.Miss(5, 4)];

        for (var seed = 0; seed < 20; seed++)
        {
            var next = strategy.NextShot(shots, new Random(seed));
            Assert.Contains(next, new Coordinate[] { new(4, 5), new(5, 6) });
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    public void NextShot_TwoHitsInARow_ExtendsTheLine(int seed)
    {
        List<ShotView> shots = [ShotFactory.Hit(5, 5), ShotFactory.Hit(6, 5)];

        var next = strategy.NextShot(shots, new Random(seed));

        Assert.Contains(next, new Coordinate[] { new(4, 5), new(7, 5) });
    }

    [Fact]
    public void NextShot_VerticalLineBlockedAtOneEnd_ExtendsTheOtherEnd()
    {
        List<ShotView> shots = [ShotFactory.Miss(2, 2), ShotFactory.Hit(2, 3), ShotFactory.Hit(2, 4), ShotFactory.Hit(2, 5)];

        Assert.Equal(new Coordinate(2, 6), strategy.NextShot(shots, new Random(3)));
    }

    [Fact]
    public void NextShot_LineBlockedAtBothEnds_FallsBackToNeighbours()
    {
        List<ShotView> shots =
        [
            ShotFactory.Miss(4, 5), ShotFactory.Hit(5, 5), ShotFactory.Hit(6, 5), ShotFactory.Miss(7, 5),
        ];

        var next = strategy.NextShot(shots, new Random(3));

        Assert.Contains(next, new Coordinate[] { new(5, 4), new(5, 6), new(6, 4), new(6, 6) });
    }

    [Fact]
    public void NextShot_HitAtTheBoardEdge_StaysOnTheBoard()
    {
        List<ShotView> shots = [ShotFactory.Hit(0, 0), ShotFactory.Hit(1, 0)];

        Assert.Equal(new Coordinate(2, 0), strategy.NextShot(shots, new Random(3)));
    }

    [Fact]
    public void NextShot_HitWithEveryNeighbourShot_ReturnsToHunting()
    {
        List<ShotView> shots =
        [
            ShotFactory.Hit(5, 5),
            ShotFactory.Miss(4, 5), ShotFactory.Miss(6, 5), ShotFactory.Miss(5, 4), ShotFactory.Miss(5, 6),
        ];
        var shotCells = shots.Select(s => new Coordinate(s.Column, s.Row)).ToHashSet();

        for (var seed = 0; seed < 20; seed++)
        {
            var next = strategy.NextShot(shots, new Random(seed));
            Assert.DoesNotContain(next, shotCells);
            Assert.True(Board.Contains(next));
        }
    }

    [Fact]
    public void NextShot_NoHits_Hunts()
    {
        var free = new Coordinate(11, 11);

        Assert.Equal(free, strategy.NextShot(ShotFactory.MissesEverywhereExcept(free), new Random(1)));
    }
}
