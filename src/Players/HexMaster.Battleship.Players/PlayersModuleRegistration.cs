using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Configuration;
using HexMaster.Battleship.Players.Features.CreateTokens;
using HexMaster.Battleship.Players.Features.GetCurrentUser;
using HexMaster.Battleship.Players.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlayersModuleRegistration
{
    // Registers the Players module: token configuration (validated on start), the token services
    // and the feature handlers. Storage is registered separately by the data project (AddPlayersData).
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

        services.AddScoped<ICommandHandler<CreateTokensCommand, TokenPairResponse>, CreateTokensCommandHandler>();
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, CurrentUserResponse?>, GetCurrentUserQueryHandler>();

        return services;
    }
}
