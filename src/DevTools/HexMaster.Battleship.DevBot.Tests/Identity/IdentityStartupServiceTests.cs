using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Identity;

public sealed class IdentityStartupServiceTests
{
    [Fact]
    public async Task ExecuteAsync_UsersServiceDownAtFirst_RetriesUntilTheUserExists()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var users = new Mock<IUsersClient>();
        users.SetupSequence(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gateway warming up"))
            .ReturnsAsync(TokenFactory.Pair(userId, clock.GetUtcNow()));
        var store = new TokenStore(users.Object, clock, NullLogger<TokenStore>.Instance);
        var service = new IdentityStartupService(store, clock, NullLogger<IdentityStartupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        // The retry delay runs on the fake clock; keep moving it until the second attempt succeeds.
        for (var i = 0; i < 200 && store.CurrentUserId is null; i++)
        {
            clock.Advance(IdentityStartupService.RetryDelay);
            await Task.Delay(10);
        }

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(userId, store.CurrentUserId);
        users.Verify(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
