using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Strategies;

public sealed class ProbabilityDensityStrategyTests
{
    private readonly ProbabilityDensityStrategy strategy = new();

    [Fact]
    public void Scores_EmptyBoard_PreferCentreOverCorners()
    {
        var scores = ProbabilityDensityStrategy.Scores(new ShotGrid([]));

        Assert.True(scores[5, 5] > scores[0, 0]);
        Assert.True(scores[6, 6] > scores[11, 11]);
        Assert.True(scores[5, 6] > scores[0, 11]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NextShot_EmptyBoard_PicksACentralCell(int seed)
    {
        var next = strategy.NextShot([], new Random(seed));

        Assert.InRange(next.Column, 2, 9);
        Assert.InRange(next.Row, 2, 9);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void NextShot_NeverPicksAShotCell(int seed)
    {
        var random = new Random(seed);
        var shots = new List<ShotView>();

        for (var i = 0; i < Board.Size * Board.Size; i++)
        {
            var next = strategy.NextShot(shots, random);

            Assert.DoesNotContain(shots, s => s.Column == next.Column && s.Row == next.Row);
            shots.Add(i % 9 == 0 ? ShotFactory.Hit(next.Column, next.Row) : ShotFactory.Miss(next.Column, next.Row));
        }
    }

    [Fact]
    public void NextShot_AfterAHit_PicksAnAdjacentCell()
    {
        List<ShotView> shots = [ShotFactory.Miss(1, 1), ShotFactory.Hit(5, 5)];

        for (var seed = 0; seed < 20; seed++)
        {
            var next = strategy.NextShot(shots, new Random(seed));
            Assert.Contains(next, new Coordinate[] { new(4, 5), new(6, 5), new(5, 4), new(5, 6) });
        }
    }

    [Fact]
    public void NextShot_OnlyAnIsolatedCellLeft_StillPicksIt()
    {
        var free = new Coordinate(3, 3);

        Assert.Equal(free, strategy.NextShot(ShotFactory.MissesEverywhereExcept(free), new Random(1)));
    }

    [Fact]
    public void For_EveryPreset_ReturnsItsStrategy()
    {
        Assert.IsType<RandomStrategy>(ShotStrategies.For(BotIntelligence.Random));
        Assert.IsType<HuntAndTargetStrategy>(ShotStrategies.For(BotIntelligence.HuntAndTarget));
        Assert.IsType<HuntWithParityStrategy>(ShotStrategies.For(BotIntelligence.HuntWithParity));
        Assert.IsType<ProbabilityDensityStrategy>(ShotStrategies.For(BotIntelligence.ProbabilityDensity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShotStrategies.For((BotIntelligence)99));
    }
}
