using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using HexMaster.Battleship.Aspire.AppHost;
using HexMaster.Battleship.Aspire.AppHost.DevBot;

namespace HexMaster.Battleship.Aspire.Tests.DevBot;

public sealed class BattleshipBotResourceTests
{
    [Fact]
    public async Task AppHost_RunMode_AddsTheDevBot()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(typeof(GatewayRoutes), [], (_, _) => { });

        var bot = Assert.Single(builder.Resources.OfType<BattleshipBotResource>());

        Assert.Equal("devbot", bot.Name);
        var icon = Assert.Single(bot.Annotations.OfType<ResourceIconAnnotation>());
        Assert.Equal("Bot", icon.IconName);
        Assert.Contains(bot.Annotations.OfType<ManifestPublishingCallbackAnnotation>(), a => a == ManifestPublishingCallbackAnnotation.Ignore);
        Assert.Contains(bot.Annotations.OfType<HealthCheckAnnotation>(), _ => true);
        Assert.Contains(bot.Annotations.OfType<WaitAnnotation>(), w => w.Resource.Name == "gateway");
        Assert.Contains(builder.Resources, r => r.Name.StartsWith("devbot-dapr", StringComparison.Ordinal));

        var environment = await Environment(bot);
        Assert.Equal("HuntAndTarget", environment["DevBot__Intelligence"]);
        Assert.Equal("00:00:10", environment["DevBot__JoinGracePeriod"]);
        Assert.Equal("00:00:01", environment["DevBot__ShotDelay"]);
    }

    [Fact]
    public async Task AppHost_PublishMode_HasNoDevBot()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(GatewayRoutes), ["--operation", "publish", "--publisher", "manifest", "--output-path", Path.GetTempPath()], (_, _) => { });

        Assert.True(builder.ExecutionContext.IsPublishMode);
        Assert.DoesNotContain(builder.Resources, r => r is BattleshipBotResource || r.Name.StartsWith("devbot", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("random", "Random")]
    [InlineData("ProbabilityDensity", "ProbabilityDensity")]
    public async Task WithIntelligence_SetsThePresetByItsName(string requested, string expected)
    {
        var builder = DistributedApplication.CreateBuilder();
        var bot = builder.AddResource(new BattleshipBotResource("devbot")).WithIntelligence(requested);

        Assert.Equal(expected, (await Environment(bot.Resource))["DevBot__Intelligence"]);
    }

    [Fact]
    public void WithIntelligence_UnknownPreset_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var bot = builder.AddResource(new BattleshipBotResource("devbot"));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => bot.WithIntelligence("Genius"));
        Assert.Contains("Random, HuntAndTarget, HuntWithParity, ProbabilityDensity", exception.Message);
    }

    [Fact]
    public async Task WithJoinGracePeriodAndShotDelay_SetTheirVariables()
    {
        var builder = DistributedApplication.CreateBuilder();
        var bot = builder.AddResource(new BattleshipBotResource("devbot"))
            .WithJoinGracePeriod(TimeSpan.FromSeconds(30))
            .WithShotDelay(TimeSpan.FromMilliseconds(250));

        var environment = await Environment(bot.Resource);

        Assert.Equal("00:00:30", environment["DevBot__JoinGracePeriod"]);
        Assert.Equal("00:00:00.2500000", environment["DevBot__ShotDelay"]);
    }

    [Fact]
    public void WithShotDelay_Negative_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var bot = builder.AddResource(new BattleshipBotResource("devbot"));

        Assert.Throws<ArgumentOutOfRangeException>(() => bot.WithShotDelay(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => bot.WithJoinGracePeriod(TimeSpan.FromSeconds(-1)));
    }

    // Runs only the environment callbacks that set plain values; the ones the project defaults add
    // need a running application and are skipped.
    private static async Task<Dictionary<string, string>> Environment(IResource resource)
    {
        var values = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), values);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            try
            {
                await callback.Callback(context);
            }
            catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException)
            {
            }
        }

        return values
            .Where(v => v.Key.StartsWith("DevBot__", StringComparison.Ordinal))
            .ToDictionary(v => v.Key, v => v.Value?.ToString() ?? string.Empty);
    }
}
