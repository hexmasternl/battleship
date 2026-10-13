using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.DomainModels;
using HexMaster.Battleship.Players.Observability;
using HexMaster.Battleship.Players.Services;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Players.Features.CreateTokens;

public sealed class CreateTokensCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    CallSignGenerator callSigns,
    TimeProvider time,
    PlayersMetrics metrics,
    ILogger<CreateTokensCommandHandler> logger) : ICommandHandler<CreateTokensCommand, TokenPairResponse>
{
    public async Task<TokenPairResponse> Handle(CreateTokensCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNowInSeconds();
        var displayName = string.IsNullOrWhiteSpace(command.DisplayName) ? callSigns.Generate() : command.DisplayName;

        var user = User.Create(displayName, now);
        Activity.Current?.SetTag(PlayersTelemetry.PlayerIdAttribute, user.Id.ToString());
        await users.SaveAsync(user, cancellationToken);

        var issued = tokens.Issue(user, now);
        await refreshTokens.SaveAsync(issued.StoredRefreshToken, cancellationToken);

        metrics.TokensIssued();
        PlayersLog.TokensIssued(logger, user.Id);

        return issued.ToResponse();
    }
}
