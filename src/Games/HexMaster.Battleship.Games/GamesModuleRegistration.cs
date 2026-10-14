using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Features.CleanUpInactiveGames;
using HexMaster.Battleship.Games.Features.CreateGame;
using HexMaster.Battleship.Games.Features.FireShot;
using HexMaster.Battleship.Games.Features.GetGame;
using HexMaster.Battleship.Games.Features.JoinGame;
using HexMaster.Battleship.Games.Features.MarkReady;
using HexMaster.Battleship.Games.Features.PlaceShip;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class GamesModuleRegistration
{
    // Registers the Games module: code generation, telemetry and the feature handlers, each wrapped
    // in its use-case span by the Core decorator. Storage is registered separately by the data
    // project (AddGamesData), and the integration event sender by AddIntegrationEvents().
    public static IServiceCollection AddGamesModule(this IServiceCollection services)
    {
        services.AddOptions<GameRetentionOptions>()
            .BindConfiguration(GameRetentionOptions.ConfigurationSection)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<GameRetentionOptions>, GameRetentionOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.TryAddSingleton<GamesMetrics>();
        services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();
        services.TryAddScoped<IGameUpdater, GameUpdater>();
        services.TryAddSingleton<IFirstTurnPicker, RandomFirstTurnPicker>();

        services.AddCommandHandler<CreateGameCommand, CreateGameResponse, CreateGameCommandHandler>();
        services.AddCommandHandler<JoinGameCommand, JoinGameResult, JoinGameCommandHandler>();
        services.AddQueryHandler<GetGameQuery, GameView?, GetGameQueryHandler>();
        services.AddCommandHandler<PlaceShipCommand, GameView?, PlaceShipCommandHandler>();
        services.AddCommandHandler<MarkReadyCommand, GameView?, MarkReadyCommandHandler>();
        services.AddCommandHandler<FireShotCommand, FireShotResult, FireShotCommandHandler>();
        services.AddCommandHandler<CleanUpInactiveGamesCommand, CleanUpInactiveGamesResult, CleanUpInactiveGamesCommandHandler>();

        return services;
    }
}
