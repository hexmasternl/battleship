using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.IntegrationEvents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Integration;

// The Games API as it runs, validating tokens with the test signing key, with storage and the event
// sender replaced by mocks so the tests need neither Azurite nor a Dapr sidecar.
public sealed class GamesApiFactory : WebApplicationFactory<Program>
{
    public Mock<IGameRepository> Games { get; } = new();

    public Mock<IIntegrationEventSender> Events { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Users:Tokens:SigningKey", TestTokens.SigningKey);
        builder.UseSetting($"ConnectionStrings:{Microsoft.Extensions.Hosting.GamesDataRegistration.TablesConnectionName}", "UseDevelopmentStorage=true");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Games.Object);
            services.AddSingleton(Events.Object);
        });
    }
}
