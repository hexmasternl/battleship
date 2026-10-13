using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Users.Configuration;
using HexMaster.Battleship.Users.Features.CreateTokens;
using HexMaster.Battleship.Users.Features.GetCurrentUser;
using HexMaster.Battleship.Users.Features.RefreshTokens;
using HexMaster.Battleship.Users.Observability;
using HexMaster.Battleship.Users.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class UsersModuleRegistration
{
    // Registers the Users module: token configuration (validated on start), the token services,
    // telemetry and the feature handlers, each wrapped in its use-case span by the Core decorator.
    // Storage is registered separately by the data project (AddUsersData).
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
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
        services.TryAddSingleton<UsersMetrics>();

        services.AddCommandHandler<CreateTokensCommand, TokenPairResponse, CreateTokensCommandHandler>();
        services.AddCommandHandler<RefreshTokensCommand, TokenPairResponse, RefreshTokensCommandHandler>();
        services.AddQueryHandler<GetCurrentUserQuery, CurrentUserResponse?, GetCurrentUserQueryHandler>();

        return services;
    }
}
