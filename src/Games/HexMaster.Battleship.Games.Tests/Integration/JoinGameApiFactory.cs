using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Tests.Fakes;
using HexMaster.Battleship.IntegrationEvents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Integration;

// The Games API with an in-memory store that keeps games between requests and honours concurrency
// tokens, so join tests can create a game, join it and read it back.
public sealed class JoinGameApiFactory : WebApplicationFactory<Program>
{
    public InMemoryGameRepository Games { get; } = new();

    public Mock<IIntegrationEventSender> Events { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Players:Tokens:SigningKey", TestTokens.SigningKey);
        builder.UseSetting($"ConnectionStrings:{Microsoft.Extensions.Hosting.GamesDataRegistration.TablesConnectionName}", "UseDevelopmentStorage=true");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IGameRepository>(Games);
            services.AddSingleton(Events.Object);
        });
    }
}
