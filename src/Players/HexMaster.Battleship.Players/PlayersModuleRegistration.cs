using HexMaster.Battleship.Players.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlayersModuleRegistration
{
    // Registers the Players module: token configuration (validated on start) and the feature
    // handlers. Storage is registered separately by the data project (AddPlayersData).
    public static IServiceCollection AddPlayersModule(this IServiceCollection services)
    {
        services.AddOptions<TokenOptions>()
            .BindConfiguration(TokenOptions.ConfigurationSection)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TokenOptions>, TokenOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
