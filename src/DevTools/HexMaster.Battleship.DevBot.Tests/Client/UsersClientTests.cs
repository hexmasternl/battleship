using System.Net;
using System.Text.Json;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.DevBot.Tests.Client;

public sealed class UsersClientTests
{
    private static readonly TokenPairResponse Tokens = new("access", DateTimeOffset.UtcNow.AddMinutes(15), "refresh", DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task CreateTokensAsync_PostsTheDisplayName()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.JsonResponse(HttpStatusCode.Created, Tokens));

        var tokens = await new UsersClient(handler.CreateClient()).CreateTokensAsync("Dev Bot", CancellationToken.None);

        Assert.Equal("access", tokens.AccessToken);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("http://gateway/users/tokens", request.RequestUri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("Dev Bot", json.RootElement.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task CreateTokensAsync_Failure_Throws()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new UsersClient(handler.CreateClient()).CreateTokensAsync("Dev Bot", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsync_Ok_ReturnsTheNewPair()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.JsonResponse(HttpStatusCode.OK, Tokens));

        var tokens = await new UsersClient(handler.CreateClient()).RefreshAsync("old", CancellationToken.None);

        Assert.Equal("refresh", tokens!.RefreshToken);
        Assert.Equal("http://gateway/users/tokens/refresh", handler.Requests[0].Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task RefreshAsync_Refused_ReturnsNull()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Problem(HttpStatusCode.Unauthorized, null));

        Assert.Null(await new UsersClient(handler.CreateClient()).RefreshAsync("old", CancellationToken.None));
    }
}
