using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Services;

namespace HexMaster.Battleship.Players.Features.RefreshTokens;

public sealed class RefreshTokensCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    TimeProvider time) : ICommandHandler<RefreshTokensCommand, TokenPairResponse>
{
    public async Task<TokenPairResponse> Handle(RefreshTokensCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNowInSeconds();

        var presented = await refreshTokens.GetAsync(RefreshTokenGenerator.Hash(command.RefreshToken), cancellationToken)
            ?? throw new TokenRefusedException("unknown token");
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

        return issued.ToResponse();
    }
}
