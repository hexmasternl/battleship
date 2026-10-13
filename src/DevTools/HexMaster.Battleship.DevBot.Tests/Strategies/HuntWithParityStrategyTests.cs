using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Strategies;

public sealed class HuntWithParityStrategyTests
{
    private readonly HuntWithParityStrategy strategy = new();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NextShot_WhileHunting_PicksOnlyParityCells(int seed)
    {
        var random = new Random(seed);
        var shots = new List<ShotView>();

        // All misses keep the strategy hunting; the 72 checkerboard cells come first.
        for (var i = 0; i < Board.Size * Board.Size / 2; i++)
        {
            var next = strategy.NextShot(shots, random);

            Assert.Equal(0, (next.Column + next.Row) % 2);
            shots.Add(ShotFactory.Miss(next.Column, next.Row));
        }
    }

    [Fact]
    public void NextShot_ParityCellsUsedUp_FallsBackToTheOtherCells()
    {
        var shots = ShotFactory.AllCells()
            .Where(HuntWithParityStrategy.IsParityCell)
            .Select(c => ShotFactory.Miss(c.Column, c.Row))
            .ToList();

        var next = strategy.NextShot(shots, new Random(4));

        Assert.False(HuntWithParityStrategy.IsParityCell(next));
    }

    [Fact]
    public void NextShot_WithAHit_TargetsLikeHuntAndTarget()
    {
        List<ShotView> shots = [ShotFactory.Hit(5, 5), ShotFactory.Miss(6, 5), ShotFactory.Hit(5, 6)];
        var huntAndTarget = new HuntAndTargetStrategy();

        for (var seed = 0; seed < 20; seed++)
        {
            Assert.Equal(huntAndTarget.NextShot(shots, new Random(seed)), strategy.NextShot(shots, new Random(seed)));
        }
    }

    [Fact]
    public void NextShot_HitOnANonParityCell_TargetsItsNeighbours()
    {
        List<ShotView> shots = [ShotFactory.Hit(5, 4)];

        var next = strategy.NextShot(shots, new Random(8));

        Assert.Contains(next, new Coordinate[] { new(4, 4), new(6, 4), new(5, 3), new(5, 5) });
    }
}
