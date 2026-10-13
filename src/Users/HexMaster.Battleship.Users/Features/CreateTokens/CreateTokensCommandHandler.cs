using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Users.Abstractions.Repositories;
using HexMaster.Battleship.Users.DomainModels;
using HexMaster.Battleship.Users.Observability;
using HexMaster.Battleship.Users.Services;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Users.Features.CreateTokens;

public sealed class CreateTokensCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    CallSignGenerator callSigns,
    TimeProvider time,
    UsersMetrics metrics,
    ILogger<CreateTokensCommandHandler> logger) : ICommandHandler<CreateTokensCommand, TokenPairResponse>
{
    public async Task<TokenPairResponse> Handle(CreateTokensCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNowInSeconds();
        var displayName = string.IsNullOrWhiteSpace(command.DisplayName) ? callSigns.Generate() : command.DisplayName;

        var user = User.Create(displayName, now);
        Activity.Current?.SetTag(UsersTelemetry.UserIdAttribute, user.Id.ToString());
        await users.SaveAsync(user, cancellationToken);

        var issued = tokens.Issue(user, now);
        await refreshTokens.SaveAsync(issued.StoredRefreshToken, cancellationToken);

        metrics.TokensIssued();
        UsersLog.TokensIssued(logger, user.Id);

        return issued.ToResponse();
    }
}
