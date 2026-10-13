using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Identity;

public sealed class TokenStoreTests
{
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IUsersClient> users = new(MockBehavior.Strict);
    private readonly Guid userId = Guid.NewGuid();
    private readonly TokenStore store;

    public TokenStoreTests()
    {
        store = new TokenStore(users.Object, clock, NullLogger<TokenStore>.Instance);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FirstCall_CreatesTheDevBotUser()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow());
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>())).ReturnsAsync(first);

        var token = await store.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(first.AccessToken, token);
        Assert.Equal(userId, await store.GetUserIdAsync(CancellationToken.None));
        Assert.Equal(userId, store.CurrentUserId);
        users.Verify(u => u.CreateTokensAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CurrentUserId_BeforeTheUserExists_IsNull()
    {
        Assert.Null(store.CurrentUserId);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WellBeforeExpiry_ReusesTheToken()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow());
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>())).ReturnsAsync(first);
        await store.GetAccessTokenAsync(CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(13));
        var token = await store.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(first.AccessToken, token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_OneMinuteBeforeExpiry_Refreshes()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow(), "1");
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>())).ReturnsAsync(first);
        await store.GetAccessTokenAsync(CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(14));
        var second = TokenFactory.Pair(userId, clock.GetUtcNow(), "2");
        users.Setup(u => u.RefreshAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(second);

        var token = await store.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(second.AccessToken, token);
        Assert.Equal(userId, store.CurrentUserId);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshRefused_CreatesANewUser()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow(), "1");
        var newUser = Guid.NewGuid();
        users.SetupSequence(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(first)
            .ReturnsAsync(TokenFactory.Pair(newUser, clock.GetUtcNow().AddMinutes(20), "new"));
        users.Setup(u => u.RefreshAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync((TokenPairResponse?)null);
        await store.GetAccessTokenAsync(CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(20));
        await store.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(newUser, store.CurrentUserId);
    }

    [Fact]
    public async Task RenewAsync_RefusedCurrentToken_RefreshesOnce()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow(), "1");
        var second = TokenFactory.Pair(userId, clock.GetUtcNow(), "2");
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>())).ReturnsAsync(first);
        users.Setup(u => u.RefreshAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(second);
        await store.GetAccessTokenAsync(CancellationToken.None);

        var renewed = await store.RenewAsync(first.AccessToken, CancellationToken.None);
        var again = await store.RenewAsync(first.AccessToken, CancellationToken.None);

        Assert.Equal(second.AccessToken, renewed);
        Assert.Equal(second.AccessToken, again);
        users.Verify(u => u.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenewAsync_BeforeAnyUser_CreatesOne()
    {
        var first = TokenFactory.Pair(userId, clock.GetUtcNow());
        users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>())).ReturnsAsync(first);

        Assert.Equal(first.AccessToken, await store.RenewAsync("whatever", CancellationToken.None));
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJuYW1lIjoiRGV2IEJvdCJ9.sig")]
    public void ReadSubject_InvalidToken_Throws(string token)
    {
        Assert.Throws<InvalidOperationException>(() => TokenStore.ReadSubject(token));
    }
}
