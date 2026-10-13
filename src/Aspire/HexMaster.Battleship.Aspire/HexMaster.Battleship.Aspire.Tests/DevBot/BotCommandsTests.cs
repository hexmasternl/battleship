using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using HexMaster.Battleship.Aspire.AppHost.DevBot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.Battleship.Aspire.Tests.DevBot;

public sealed class BotCommandsTests
{
    private readonly List<(HttpRequestMessage Request, string? Body)> requests = [];
    private Func<HttpRequestMessage, HttpResponseMessage> respond = _ => new HttpResponseMessage(HttpStatusCode.OK);

    [Fact]
    public void WithBotCommandSet_RegistersTheFourCommands()
    {
        var bot = CreateBot();

        var names = bot.Resource.Annotations.OfType<ResourceCommandAnnotation>().Select(c => c.Name).ToList();

        Assert.Equal([BotCommands.SetIntelligence, BotCommands.EnableAutoJoin, BotCommands.DisableAutoJoin, BotCommands.JoinGame], names);
        var setIntelligence = Command(bot, BotCommands.SetIntelligence);
        var input = Assert.Single(setIntelligence.Arguments!);
        Assert.Equal(InputType.Choice, input.InputType);
        Assert.True(input.Required);
        Assert.Equal(["Random", "HuntAndTarget", "HuntWithParity", "ProbabilityDensity"], input.Options!.Select(o => o.Key));
        var code = Assert.Single(Command(bot, BotCommands.JoinGame).Arguments!);
        Assert.Equal(8, code.MaxLength);
        Assert.True(code.Required);
    }

    [Fact]
    public async Task SetIntelligence_Success_PutsThePresetAndReportsSuccess()
    {
        var bot = CreateBot();

        var result = await Execute(bot, BotCommands.SetIntelligence, (BotCommands.IntelligenceArgument, "HuntWithParity"));

        Assert.True(result.Success);
        var (request, body) = Assert.Single(requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("http://devbot/admin/intelligence", request.RequestUri!.ToString());
        Assert.Equal("HuntWithParity", JsonDocument.Parse(body!).RootElement.GetProperty("intelligence").GetString());
    }

    [Fact]
    public async Task SetIntelligence_ProblemResponse_ReportsItsDetail()
    {
        respond = _ => Problem(HttpStatusCode.BadRequest, "'Genius' is not a preset; use one of Random, HuntAndTarget, HuntWithParity, ProbabilityDensity.");
        var bot = CreateBot();

        var result = await Execute(bot, BotCommands.SetIntelligence, (BotCommands.IntelligenceArgument, "Genius"));

        Assert.False(result.Success);
        Assert.Contains("Random, HuntAndTarget, HuntWithParity, ProbabilityDensity", result.Message);
    }

    [Theory]
    [InlineData(BotCommands.EnableAutoJoin, true)]
    [InlineData(BotCommands.DisableAutoJoin, false)]
    public async Task AutoJoinCommands_PutTheSetting(string command, bool enabled)
    {
        var bot = CreateBot();

        var result = await Execute(bot, command);

        Assert.True(result.Success);
        var (request, body) = Assert.Single(requests);
        Assert.Equal("http://devbot/admin/auto-join", request.RequestUri!.ToString());
        Assert.Equal(enabled, JsonDocument.Parse(body!).RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task JoinGame_Success_PostsTheUpperCaseCode()
    {
        var bot = CreateBot();

        var result = await Execute(bot, BotCommands.JoinGame, (BotCommands.CodeArgument, " k7q2m9xa "));

        Assert.True(result.Success);
        Assert.Equal("http://devbot/admin/games/K7Q2M9XA", requests[0].Request.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, requests[0].Request.Method);
    }

    [Fact]
    public async Task JoinGame_FullGame_ReportsTheMessage()
    {
        respond = _ => Problem(HttpStatusCode.Conflict, "Game K7Q2M9XA is full: it already has two other players.");
        var bot = CreateBot();

        var result = await Execute(bot, BotCommands.JoinGame, (BotCommands.CodeArgument, "K7Q2M9XA"));

        Assert.False(result.Success);
        Assert.Equal("Game K7Q2M9XA is full: it already has two other players.", result.Message);
    }

    [Fact]
    public async Task Command_ErrorWithoutProblemBody_ReportsTheStatus()
    {
        respond = _ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>") };
        var bot = CreateBot();

        var result = await Execute(bot, BotCommands.EnableAutoJoin);

        Assert.False(result.Success);
        Assert.Contains("502", result.Message);
    }

    [Fact]
    public async Task Command_BotUnreachable_ReportsFailure()
    {
        respond = _ => throw new HttpRequestException("Connection refused");
        var bot = CreateBot();

        var result = await Execute(bot, BotCommands.DisableAutoJoin);

        Assert.False(result.Success);
        Assert.Contains("Connection refused", result.Message);
    }

    [Theory]
    [InlineData("K7Q2M9XA", true)]
    [InlineData("k7q2m9xa", true)]
    [InlineData("K7Q2", false)]
    [InlineData("K7Q2-9XA", false)]
    [InlineData("", false)]
    public void CodeError_AcceptsOnlyEightLettersAndDigits(string code, bool valid)
    {
        Assert.Equal(valid, BotCommands.CodeError(code) is null);
    }

    [Fact]
    public void JoinGame_ValidatesItsCode()
    {
        var validate = Command(CreateBot(), BotCommands.JoinGame).ValidateArguments;

        Assert.NotNull(validate);
        Assert.Equal(nameof(BotCommands.ValidateCode), validate.Method.Name);
    }

    [Theory]
    [InlineData(true, ResourceCommandState.Enabled)]
    [InlineData(false, ResourceCommandState.Disabled)]
    public void WhenHealthy_FollowsTheHealth(bool healthy, ResourceCommandState expected)
    {
        Assert.Equal(expected, BotCommands.WhenHealthy(StateContext(healthy)));
    }

    [Theory]
    [InlineData(true, true, ResourceCommandState.Disabled)]
    [InlineData(true, false, ResourceCommandState.Enabled)]
    [InlineData(false, true, ResourceCommandState.Enabled)]
    [InlineData(false, false, ResourceCommandState.Disabled)]
    public void AutoJoinCommandState_EnablesOnlyTheUsefulCommand(bool autoJoin, bool enables, ResourceCommandState expected)
    {
        var context = StateContext(healthy: true, new ResourcePropertySnapshot(BattleshipBotResource.AutoJoinProperty, autoJoin));

        Assert.Equal(expected, BotCommands.AutoJoinCommandState(context, enables));
    }

    [Fact]
    public void AutoJoinCommandState_SettingNotKnownYet_EnablesBoth()
    {
        Assert.Equal(ResourceCommandState.Enabled, BotCommands.AutoJoinCommandState(StateContext(healthy: true), enables: true));
        Assert.Equal(ResourceCommandState.Enabled, BotCommands.AutoJoinCommandState(StateContext(healthy: true), enables: false));
    }

    [Fact]
    public void AutoJoinCommandState_Unhealthy_DisablesBoth()
    {
        var context = StateContext(healthy: false, new ResourcePropertySnapshot(BattleshipBotResource.AutoJoinProperty, false));

        Assert.Equal(ResourceCommandState.Disabled, BotCommands.AutoJoinCommandState(context, enables: true));
    }

    private IResourceBuilder<BattleshipBotResource> CreateBot()
    {
        var builder = DistributedApplication.CreateBuilder();
        var handler = new StubHandler(this);
        return builder.AddResource(new BattleshipBotResource("devbot"))
            .WithBotCommandSet(_ => new BotAdminClient(new HttpClient(handler) { BaseAddress = new Uri("http://devbot/") }));
    }

    private static ResourceCommandAnnotation Command(IResourceBuilder<BattleshipBotResource> bot, string name) =>
        bot.Resource.Annotations.OfType<ResourceCommandAnnotation>().Single(c => c.Name == name);

    private static Task<ExecuteCommandResult> Execute(
        IResourceBuilder<BattleshipBotResource> bot, string name, params (string Name, string Value)[] arguments)
    {
        var command = Command(bot, name);
        var inputs = (command.Arguments ?? []).ToList();
        foreach (var (argument, value) in arguments)
        {
            inputs.Single(i => i.Name == argument).Value = value;
        }

        return command.ExecuteCommand(new ExecuteCommandContext
        {
            Services = new ServiceCollection().BuildServiceProvider(),
            ResourceName = "devbot",
            CancellationToken = CancellationToken.None,
            Logger = NullLogger.Instance,
            Arguments = new InteractionInputCollection(inputs),
        });
    }

    private static UpdateCommandStateContext StateContext(bool healthy, params ResourcePropertySnapshot[] properties) => new()
    {
        Services = new ServiceCollection().BuildServiceProvider(),
        ResourceSnapshot = new CustomResourceSnapshot
        {
            ResourceType = "Project",
            State = KnownResourceStates.Running,
            Properties = [.. properties],
        }.WithHealthReports([new HealthReportSnapshot("devbot_http_/health_200_check", healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy, null, null)]),
    };

    private static HttpResponseMessage Problem(HttpStatusCode status, string detail) => new(status)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(new { title = "Refused", detail, status = (int)status }), Encoding.UTF8, "application/problem+json"),
    };

    private sealed class StubHandler(BotCommandsTests owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            owner.requests.Add((request, body));
            return owner.respond(request);
        }
    }
}
