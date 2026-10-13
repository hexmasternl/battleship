using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class PlayersDataRegistration
{
    // The name of the Aspire tables resource the AppHost passes to the Players API.
    public const string TablesConnectionName = "tables";

    // Registers the Azure Table Storage repositories over the TableServiceClient that the Aspire
    // client integration builds from the "tables" connection (Azurite locally).
    public static IHostApplicationBuilder AddPlayersData(this IHostApplicationBuilder builder)
    {
        builder.AddAzureTableServiceClient(TablesConnectionName);

        builder.Services.AddSingleton<IUserRepository, UserTableRepository>();
        builder.Services.AddSingleton<IRefreshTokenRepository, RefreshTokenTableRepository>();

        return builder;
    }
}
