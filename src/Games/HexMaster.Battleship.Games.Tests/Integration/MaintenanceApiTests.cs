using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Endpoints;
using HexMaster.Battleship.Games.Tests.Factories;

namespace HexMaster.Battleship.Games.Tests.Integration;

public class MaintenanceApiTests(JoinGameApiFactory factory) : IClassFixture<JoinGameApiFactory>
{
    [Fact]
    public async Task GameCleanup_WithoutATokenRemovesTheInactiveGameAndKeepsTheRecentOne()
    {
        var now = DateTimeOffset.UtcNow;
        var old = GameFactory.Stored(GameState.Started, lastActivityOn: now.AddDays(-100));
        var recent = GameFactory.Stored(GameState.Started, lastActivityOn: now.AddDays(-1));
        await factory.Games.TryAddAsync(old, CancellationToken.None);
        await factory.Games.TryAddAsync(recent, CancellationToken.None);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/maintenance/game-cleanup", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("removed").GetInt32());
        Assert.Equal(0, body.GetProperty("skipped").GetInt32());
        Assert.False(factory.Games.Contains(old.GameCode));
        Assert.True(factory.Games.Contains(recent.GameCode));

        using var read = new HttpRequestMessage(HttpMethod.Get, $"/games/{old.GameCode}");
        read.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Issue(old.PlayerOne.PlayerId, old.PlayerOne.PlayerName));
        using var gone = await client.SendAsync(read);

        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        var problem = await gone.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(GameProblemTypes.GameNotFound, problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GameCleanup_AcceptsTheCronBindingPayload()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/maintenance/game-cleanup", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
