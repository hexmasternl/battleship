using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Observability;
using HexMaster.Battleship.Players.Services;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Players.Features.RefreshTokens;

public sealed class RefreshTokensCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    TimeProvider time,
    PlayersMetrics metrics,
    ILogger<RefreshTokensCommandHandler> logger) : ICommandHandler<RefreshTokensCommand, TokenPairResponse>
{
    public async Task<TokenPairResponse> Handle(RefreshTokensCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return await RefreshAsync(command, cancellationToken);
        }
        catch (TokenRefusedException exception)
        {
            // Only the bounded reason is recorded; the presented token never is.
            var reason = TokenRefusalReasons.Of(exception.Reason);
            Activity.Current?.SetTag(PlayersTelemetry.RefusalReasonAttribute, reason);
            metrics.TokenRefused(reason);
            PlayersLog.TokenRefused(logger, reason);

            throw;
        }
    }

    private async Task<TokenPairResponse> RefreshAsync(RefreshTokensCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNowInSeconds();

        var presented = await refreshTokens.GetAsync(RefreshTokenGenerator.Hash(command.RefreshToken), cancellationToken)
            ?? throw new TokenRefusedException("unknown token");
        Activity.Current?.SetTag(PlayersTelemetry.PlayerIdAttribute, presented.UserId.ToString());
        if (!presented.IsActive(now))
        {
            throw new TokenRefusedException(presented.ConsumedAt is null ? "expired token" : "token used already");
        }

        var user = await users.GetAsync(presented.UserId, cancellationToken)
            ?? throw new TokenRefusedException("unknown user");
        if (user.IsRevoked)
        {
            throw new TokenRefusedException("revoked user");
        }

        // Consuming first, conditioned on the row being unchanged, closes the replay window: of two
        // concurrent requests with the same token only one gets past this point.
        try
        {
            presented.Consume(now);
            await refreshTokens.SaveAsync(presented, cancellationToken);
        }
        catch (Exception exception) when (exception is DomainException or ConcurrencyConflictException)
        {
            throw new TokenRefusedException("token used already");
        }

        var issued = tokens.Issue(user, now);
        await refreshTokens.SaveAsync(issued.StoredRefreshToken, cancellationToken);

        user.Touch(now);
        await users.SaveAsync(user, cancellationToken);

        metrics.TokensRefreshed();
        PlayersLog.TokensRefreshed(logger, user.Id);

        return issued.ToResponse();
    }
}
