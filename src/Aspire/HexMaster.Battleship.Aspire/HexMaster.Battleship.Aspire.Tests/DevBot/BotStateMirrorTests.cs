using Aspire.Hosting.ApplicationModel;
using HexMaster.Battleship.Aspire.AppHost.DevBot;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.Battleship.Aspire.Tests.DevBot;

public sealed class BotStateMirrorTests
{
    private readonly Queue<object?> answers = new();
    private CustomResourceSnapshot snapshot = new()
    {
        ResourceType = "Project",
        Properties = [new ResourcePropertySnapshot("project.path", "DevBot.csproj")],
    };
    private int publishes;

    [Fact]
    public async Task PollOnceAsync_ChangedState_PublishesTheProperties()
    {
        var mirror = CreateMirror();
        answers.Enqueue(new BotState("Random", true, ["K7Q2M9XA"]));

        Assert.True(await mirror.PollOnceAsync(CancellationToken.None));

        Assert.Equal(1, publishes);
        Assert.Equal("Random", Property(BattleshipBotResource.IntelligenceProperty));
        Assert.Equal(true, Property(BattleshipBotResource.AutoJoinProperty));
        Assert.Equal("K7Q2M9XA", Property(BattleshipBotResource.GamesProperty));
        Assert.Equal("DevBot.csproj", Property("project.path"));
    }

    [Fact]
    public async Task PollOnceAsync_SameStateAgain_PublishesNothing()
    {
        var mirror = CreateMirror();
        answers.Enqueue(new BotState("Random", true, ["K7Q2M9XA"]));
        answers.Enqueue(new BotState("Random", true, ["K7Q2M9XA"]));

        await mirror.PollOnceAsync(CancellationToken.None);
        Assert.False(await mirror.PollOnceAsync(CancellationToken.None));

        Assert.Equal(1, publishes);
    }

    [Fact]
    public async Task PollOnceAsync_GameEnded_DropsItFromTheGames()
    {
        var mirror = CreateMirror();
        answers.Enqueue(new BotState("ProbabilityDensity", false, ["AAAA1111", "K7Q2M9XA"]));
        answers.Enqueue(new BotState("ProbabilityDensity", false, ["AAAA1111"]));

        await mirror.PollOnceAsync(CancellationToken.None);
        Assert.Equal("AAAA1111, K7Q2M9XA", Property(BattleshipBotResource.GamesProperty));
        Assert.True(await mirror.PollOnceAsync(CancellationToken.None));

        Assert.Equal("AAAA1111", Property(BattleshipBotResource.GamesProperty));
        Assert.Equal(false, Property(BattleshipBotResource.AutoJoinProperty));
        Assert.Single(snapshot.Properties, p => p.Name == BattleshipBotResource.GamesProperty);
    }

    [Fact]
    public async Task PollOnceAsync_NoGames_ShowsNone()
    {
        var mirror = CreateMirror();
        answers.Enqueue(new BotState("HuntAndTarget", true, []));

        await mirror.PollOnceAsync(CancellationToken.None);

        Assert.Equal(BotStateMirror.NoGames, Property(BattleshipBotResource.GamesProperty));
    }

    [Fact]
    public async Task PollOnceAsync_BotNotAnswering_KeepsTheProperties()
    {
        var mirror = CreateMirror();
        answers.Enqueue(new BotState("Random", true, []));
        answers.Enqueue(new HttpRequestException("Connection refused"));
        answers.Enqueue(null);

        await mirror.PollOnceAsync(CancellationToken.None);
        Assert.False(await mirror.PollOnceAsync(CancellationToken.None));
        Assert.False(await mirror.PollOnceAsync(CancellationToken.None));

        Assert.Equal(1, publishes);
        Assert.Equal("Random", Property(BattleshipBotResource.IntelligenceProperty));
    }

    private BotStateMirror CreateMirror() => new(
        _ => answers.Dequeue() switch
        {
            Exception exception => throw exception,
            var state => Task.FromResult((BotState?)state),
        },
        update =>
        {
            snapshot = update(snapshot);
            publishes++;
            return Task.CompletedTask;
        },
        TimeProvider.System,
        NullLogger.Instance);

    private object? Property(string name) => snapshot.Properties.Single(p => p.Name == name).Value;
}
