using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Features.CreateGame;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class GamesModuleRegistration
{
    // Registers the Games module: code generation, telemetry and the feature handlers, each wrapped
    // in its use-case span by the Core decorator. Storage is registered separately by the data
    // project (AddGamesData), and the integration event sender by AddIntegrationEvents().
    public static IServiceCollection AddGamesModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.TryAddSingleton<GamesMetrics>();
        services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();
        services.TryAddScoped<IGameUpdater, GameUpdater>();

        services.AddCommandHandler<CreateGameCommand, CreateGameResponse, CreateGameCommandHandler>();

        return services;
    }
}
