using System.Net;
using System.Net.Http.Json;
using HexMaster.Battleship.DevBot.Admin;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Sessions;
using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry.Instrumentation.AspNetCore;

namespace HexMaster.Battleship.DevBot.Tests.Endpoints;

public sealed class AdminEndpointsTests : IAsyncLifetime
{
    private readonly DevBotApplicationFactory factory = new();
    private readonly string code = GameViewFactory.Code();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task GetState_AtStart_ShowsTheDefaults()
    {
        using var client = factory.CreateClient();

        var state = await client.GetFromJsonAsync<AdminStateResponse>("/admin/state");

        Assert.Equal("HuntAndTarget", state!.Intelligence);
        Assert.True(state.AutoJoin);
        Assert.Empty(state.Games);
    }

    [Theory]
    [InlineData("Random", BotIntelligence.Random)]
    [InlineData("probabilitydensity", BotIntelligence.ProbabilityDensity)]
    [InlineData(" HuntWithParity ", BotIntelligence.HuntWithParity)]
    public async Task SetIntelligence_KnownPreset_AppliesIt(string name, BotIntelligence expected)
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/admin/intelligence", new SetIntelligenceRequest(name));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<AdminStateResponse>();
        Assert.Equal(expected.ToString(), state!.Intelligence);
        Assert.Equal(expected, factory.Services.GetRequiredService<BotSettings>().Intelligence);
    }

    [Theory]
    [InlineData("Genius")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData(null)]
    public async Task SetIntelligence_UnknownPreset_Answers400ListingThePresetsAndKeepsTheActiveOne(string? name)
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/admin/intelligence", new SetIntelligenceRequest(name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Random, HuntAndTarget, HuntWithParity, ProbabilityDensity", problem!.Detail);
        Assert.Equal(BotIntelligence.HuntAndTarget, factory.Services.GetRequiredService<BotSettings>().Intelligence);
    }

    [Fact]
    public async Task SetAutoJoin_SwitchesItOffAndOn()
    {
        using var client = factory.CreateClient();

        using var off = await client.PutAsJsonAsync("/admin/auto-join", new SetAutoJoinRequest(false));
        Assert.False((await off.Content.ReadFromJsonAsync<AdminStateResponse>())!.AutoJoin);

        using var on = await client.PutAsJsonAsync("/admin/auto-join", new SetAutoJoinRequest(true));
        Assert.True((await on.Content.ReadFromJsonAsync<AdminStateResponse>())!.AutoJoin);
    }

    [Fact]
    public async Task SetAutoJoin_MissingValue_Answers400()
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/admin/auto-join", new SetAutoJoinRequest(null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JoinGame_FreeGame_JoinsAndShowsItInTheState()
    {
        factory.Games.Setup(g => g.JoinAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<JoinGameResponse>.Ok(new JoinGameResponse(Guid.NewGuid(), code)));
        factory.Games.Setup(g => g.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code, GameState.New, GameViewFactory.FullFleet(), ready: true)));
        using var client = factory.CreateClient();

        using var response = await client.PostAsync($"/admin/games/{code.ToLowerInvariant()}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JoinGameAdminResponse>())!.GameCode);
        var state = await client.GetFromJsonAsync<AdminStateResponse>("/admin/state");
        Assert.Equal([code], state!.Games);
    }

    [Theory]
    [InlineData(GameCallStatus.NotFound, HttpStatusCode.NotFound, "No game has code")]
    [InlineData(GameCallStatus.GameFull, HttpStatusCode.Conflict, "is full")]
    [InlineData(GameCallStatus.GameAlreadyStarted, HttpStatusCode.Conflict, "already started without the bot")]
    [InlineData(GameCallStatus.UpdateConflict, HttpStatusCode.BadGateway, "refused")]
    public async Task JoinGame_Refused_AnswersWithTheMatchingProblem(GameCallStatus status, HttpStatusCode expected, string message)
    {
        factory.Games.Setup(g => g.JoinAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<JoinGameResponse>.Problem(status, "no"));
        using var client = factory.CreateClient();

        using var response = await client.PostAsync($"/admin/games/{code}", null);

        Assert.Equal(expected, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains(message, problem!.Detail);
        Assert.Contains(code, problem.Detail);
    }

    [Theory]
    [InlineData("/admin/state", false)]
    [InlineData("/admin/intelligence", true)]
    [InlineData("/health", false)]
    public void TraceFilter_LeavesOutTheStatePoll(string path, bool traced)
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<AspNetCoreTraceInstrumentationOptions>>()
            .Get(Options.DefaultName);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.Equal(traced, options.Filter!(context));
    }
}
