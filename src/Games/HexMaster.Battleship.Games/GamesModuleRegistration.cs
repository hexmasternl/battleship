using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Features.CreateGame;
using HexMaster.Battleship.Games.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class GamesModuleRegistration
{
    // Registers the Games module: code generation, telemetry and the feature handlers. Storage is
    // registered separately by the data project (AddGamesData), and the integration event sender
    // by the service defaults.
    public static IServiceCollection AddGamesModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.AddSingleton<GamesTelemetry>();
        services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();

        services.AddScoped<ICommandHandler<CreateGameCommand, CreateGameResponse>, CreateGameCommandHandler>();

        return services;
    }
}
