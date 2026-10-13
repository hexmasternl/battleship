using HexMaster.Battleship.Users.Abstractions.Repositories;
using HexMaster.Battleship.Users.Data.TableStorage.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class UsersDataRegistration
{
    // The name of the Aspire tables resource the AppHost passes to the Users API.
    public const string TablesConnectionName = "tables";

    // Registers the Azure Table Storage repositories over the TableServiceClient that the Aspire
    // client integration builds from the "tables" connection (Azurite locally).
    public static IHostApplicationBuilder AddUsersData(this IHostApplicationBuilder builder)
    {
        builder.AddAzureTableServiceClient(TablesConnectionName);

        builder.Services.AddSingleton<IUserRepository, UserTableRepository>();
        builder.Services.AddSingleton<IRefreshTokenRepository, RefreshTokenTableRepository>();

        return builder;
    }
}
