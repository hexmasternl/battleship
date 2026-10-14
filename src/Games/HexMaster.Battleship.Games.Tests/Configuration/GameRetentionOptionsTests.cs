using HexMaster.Battleship.Games.Features.CleanUpInactiveGames;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Games.Tests.Configuration;

public class GameRetentionOptionsTests
{
    [Fact]
    public void Defaults_KeepGamesForNinetyDays()
    {
        var options = new GameRetentionOptions();

        Assert.Equal(90, options.InactiveDays);
        Assert.Equal(TimeSpan.FromDays(90), options.Retention);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NoPositiveRetention_FailsNamingTheConfigurationKey(int inactiveDays)
    {
        var result = new GameRetentionOptionsValidator().Validate(null, new GameRetentionOptions { InactiveDays = inactiveDays });

        Assert.True(result.Failed);
        Assert.Contains("Games:Retention:InactiveDays", result.FailureMessage);
    }

    [Fact]
    public void AddGamesModule_BindsTheRetentionFromConfiguration()
    {
        using var provider = Provider(("Games:Retention:InactiveDays", "30"));

        Assert.Equal(30, provider.GetRequiredService<IOptions<GameRetentionOptions>>().Value.InactiveDays);
    }

    [Fact]
    public void AddGamesModule_RefusesARetentionOfZero()
    {
        using var provider = Provider(("Games:Retention:InactiveDays", "0"));

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<GameRetentionOptions>>().Value);
        Assert.Contains("Games:Retention:InactiveDays", exception.Message);
    }

    private static ServiceProvider Provider(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddGamesModule()
            .BuildServiceProvider();
    }
}
