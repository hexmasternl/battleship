using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class GamesDataRegistration
{
    // The name of the Aspire tables resource the AppHost passes to the Games API.
    public const string TablesConnectionName = "tables";

    // Registers the Azure Table Storage repository over the TableServiceClient that the Aspire
    // client integration builds from the "tables" connection (Azurite locally).
    public static IHostApplicationBuilder AddGamesData(this IHostApplicationBuilder builder)
    {
        builder.AddAzureTableServiceClient(TablesConnectionName);

        builder.Services.AddSingleton<IGameRepository, GameTableRepository>();

        return builder;
    }
}
