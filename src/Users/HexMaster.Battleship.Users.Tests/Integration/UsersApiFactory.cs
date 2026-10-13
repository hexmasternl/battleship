using HexMaster.Battleship.Users.Tests.Factories;
using HexMaster.Battleship.Users.Tests.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HexMaster.Battleship.Users.Tests.Integration;

// The Users API as it runs, with a test signing key and the Azurite tables of the storage tests.
// Tests using it are AzuriteFacts, so they are skipped with a reason when Azurite is not reachable.
public sealed class UsersApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Users:Tokens:SigningKey", TokenOptionsFactory.SigningKey);
        builder.UseSetting($"ConnectionStrings:{Microsoft.Extensions.Hosting.UsersDataRegistration.TablesConnectionName}", Azurite.ConnectionString);
    }
}
