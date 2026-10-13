using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Factories;

// The worker with the gateway clients replaced by mocks and no user created at start-up.
internal sealed class DevBotApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IGamesClient> Games { get; } = new();

    public Mock<IUsersClient> Users { get; } = new();

    public Guid BotId { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DevBot:ShotDelay", "00:00:00");

        Users.Setup(u => u.CreateTokensAsync("Dev Bot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => TokenFactory.Pair(BotId, DateTimeOffset.UtcNow));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGamesClient>();
            services.RemoveAll<IUsersClient>();
            services.AddSingleton(Games.Object);
            services.AddSingleton(Users.Object);

            var startup = services.Single(d => d.ImplementationType == typeof(IdentityStartupService));
            services.Remove(startup);
        });
    }
}
