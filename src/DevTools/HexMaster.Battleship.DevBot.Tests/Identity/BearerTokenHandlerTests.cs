using System.Net;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Identity;

public sealed class BearerTokenHandlerTests
{
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IUsersClient> users = new();
    private readonly Guid userId = Guid.NewGuid();

    [Fact]
    public async Task SendAsync_AddsTheBearerToken()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow(), "1");
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>())).ReturnsAsync(first);
        var inner = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        using var client = CreateClient(inner);
        using var response = await client.GetAsync("games/ABC");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"Bearer {first.AccessToken}", inner.Requests[0].Request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task SendAsync_Unauthorized_RenewsOnceAndRetriesWithTheBody()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow(), "1");
        var second = TokenFactory.Pair(userId, clock.GetUtcNow(), "2");
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>())).ReturnsAsync(first);
        users.Setup(u => u.RefreshAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(second);
        var inner = new StubHttpHandler(request =>
            request.Headers.Authorization!.Parameter == first.AccessToken
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK));

        using var client = CreateClient(inner);
        using var response = await client.PostAsync("games/ABC/shots", new StringContent("""{"column":1,"row":2}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal(second.AccessToken, inner.Requests[1].Request.Headers.Authorization!.Parameter);
        Assert.Equal("""{"column":1,"row":2}""", inner.Requests[1].Body);
    }

    [Fact]
    public async Task SendAsync_UnauthorizedTwice_ReturnsTheSecond401()
    {
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TokenFactory.Pair(userId, clock.GetUtcNow(), "1"));
        users.Setup(u => u.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TokenFactory.Pair(userId, clock.GetUtcNow(), "2"));
        var inner = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        using var client = CreateClient(inner);
        using var response = await client.GetAsync("games/ABC");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
    }

    private HttpClient CreateClient(HttpMessageHandler inner)
    {
        var store = new TokenStore(users.Object, clock, NullLogger<TokenStore>.Instance);
        return new HttpClient(new BearerTokenHandler(store) { InnerHandler = inner }) { BaseAddress = new Uri("http://gateway/") };
    }
}
