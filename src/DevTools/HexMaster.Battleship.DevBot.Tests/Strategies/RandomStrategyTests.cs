using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Strategies;

public sealed class RandomStrategyTests
{
    private readonly RandomStrategy strategy = new();

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void NextShot_NeverRepeatsACell_UntilTheBoardIsFull(int seed)
    {
        var random = new Random(seed);
        var shots = new List<ShotView>();

        for (var i = 0; i < Board.Size * Board.Size; i++)
        {
            var next = strategy.NextShot(shots, random);

            Assert.True(Board.Contains(next));
            Assert.DoesNotContain(shots, s => s.Column == next.Column && s.Row == next.Row);
            shots.Add(ShotFactory.Miss(next.Column, next.Row));
        }

        Assert.Equal(144, shots.Count);
    }

    [Fact]
    public void NextShot_After143Shots_PicksTheLastFreeCell()
    {
        var free = new Coordinate(7, 3);
        var shots = ShotFactory.MissesEverywhereExcept(free);

        Assert.Equal(143, shots.Count);
        Assert.Equal(free, strategy.NextShot(shots, new Random(5)));
    }

    [Fact]
    public void NextShot_FullBoard_Throws()
    {
        var shots = ShotFactory.MissesEverywhereExcept();

        Assert.Throws<InvalidOperationException>(() => strategy.NextShot(shots, new Random(1)));
    }

    [Fact]
    public void For_Random_ReturnsTheRandomStrategy()
    {
        Assert.IsType<RandomStrategy>(ShotStrategies.For(BotIntelligence.Random));
    }
}
