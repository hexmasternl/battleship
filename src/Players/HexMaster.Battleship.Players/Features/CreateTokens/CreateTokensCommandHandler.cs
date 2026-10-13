using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.DomainModels;
using HexMaster.Battleship.Players.Services;

namespace HexMaster.Battleship.Players.Features.CreateTokens;

public sealed class CreateTokensCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    CallSignGenerator callSigns,
    TimeProvider time) : ICommandHandler<CreateTokensCommand, TokenPairResponse>
{
    public async Task<TokenPairResponse> Handle(CreateTokensCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNowInSeconds();
        var displayName = string.IsNullOrWhiteSpace(command.DisplayName) ? callSigns.Generate() : command.DisplayName;

        var user = User.Create(displayName, now);
        await users.SaveAsync(user, cancellationToken);

        var issued = tokens.Issue(user, now);
        await refreshTokens.SaveAsync(issued.StoredRefreshToken, cancellationToken);

        return issued.ToResponse();
    }
}
