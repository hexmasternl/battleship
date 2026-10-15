using System.Diagnostics;
using HexMaster.Battleship.DevBot.Placement;
using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.DevBot.Tests.Strategies;

public sealed class StrategyStrengthTests(ITestOutputHelper output)
{
    private const int Games = 200;

    [Fact]
    public void Presets_PlayedAgainstTheSameFleets_RankByAverageShotsToWin()
    {
        var stopwatch = Stopwatch.StartNew();
        var fleets = Enumerable.Range(0, Games)
            .Select(seed => RandomFleet.Create(new Random(seed)).SelectMany(s => s.Cells).ToHashSet())
            .ToList();

        var averages = Enum.GetValues<BotIntelligence>().ToDictionary(
            intelligence => intelligence,
            intelligence => fleets.Select((fleet, seed) => ShotsToWin(ShotStrategies.For(intelligence), fleet, seed + 1000)).Average());
        stopwatch.Stop();

        foreach (var (intelligence, average) in averages)
        {
            output.WriteLine($"{intelligence}: {average:F1} shots");
        }

        output.WriteLine($"Elapsed: {stopwatch.Elapsed}");

        Assert.True(averages[BotIntelligence.Random] > averages[BotIntelligence.HuntAndTarget]);
        Assert.True(averages[BotIntelligence.HuntAndTarget] > averages[BotIntelligence.HuntWithParity]);
        Assert.True(averages[BotIntelligence.HuntWithParity] >= averages[BotIntelligence.ProbabilityDensity]);
    }

    private static int ShotsToWin(IShotStrategy strategy, HashSet<Games.Abstractions.DomainModels.Coordinate> fleet, int seed)
    {
        var random = new Random(seed);
        var shots = new List<ShotView>();
        var hits = 0;

        while (hits < fleet.Count)
        {
            var next = strategy.NextShot(shots, random);
            var hit = fleet.Contains(next);
            shots.Add(new ShotView(next.Column, next.Row, hit));
            hits += hit ? 1 : 0;
        }

        return shots.Count;
    }
}
