using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Configuration;
using HexMaster.Battleship.Players.Features.CreateTokens;
using HexMaster.Battleship.Players.Features.GetCurrentUser;
using HexMaster.Battleship.Players.Features.RefreshTokens;
using HexMaster.Battleship.Players.Observability;
using HexMaster.Battleship.Players.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlayersModuleRegistration
{
    // Registers the Players module: token configuration (validated on start), the token services,
    // telemetry and the feature handlers, each wrapped in its use-case span by the Core decorator.
    // Storage is registered separately by the data project (AddPlayersData).
    public static IServiceCollection AddPlayersModule(this IServiceCollection services)
    {
        services.AddOptions<TokenOptions>()
            .BindConfiguration(TokenOptions.ConfigurationSection)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TokenOptions>, TokenOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RefreshTokenGenerator>();
        services.AddSingleton<CallSignGenerator>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddMetrics();
        services.TryAddSingleton<PlayersMetrics>();

        services.AddCommandHandler<CreateTokensCommand, TokenPairResponse, CreateTokensCommandHandler>();
        services.AddCommandHandler<RefreshTokensCommand, TokenPairResponse, RefreshTokensCommandHandler>();
        services.AddQueryHandler<GetCurrentUserQuery, CurrentUserResponse?, GetCurrentUserQueryHandler>();

        return services;
    }
}
