using HexMaster.Battleship.Players.Tests.Factories;
using HexMaster.Battleship.Players.Tests.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HexMaster.Battleship.Players.Tests.Integration;

// The Players API as it runs, with a test signing key and the Azurite tables of the storage tests.
// Tests using it are AzuriteFacts, so they are skipped with a reason when Azurite is not reachable.
public sealed class PlayersApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Players:Tokens:SigningKey", TokenOptionsFactory.SigningKey);
        builder.UseSetting($"ConnectionStrings:{Microsoft.Extensions.Hosting.PlayersDataRegistration.TablesConnectionName}", Azurite.ConnectionString);
    }
}
