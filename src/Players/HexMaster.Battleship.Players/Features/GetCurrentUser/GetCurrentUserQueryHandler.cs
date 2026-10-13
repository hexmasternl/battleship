using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Abstractions.Repositories;

namespace HexMaster.Battleship.Players.Features.GetCurrentUser;

// Returns null when the user no longer exists or was revoked, so the token no longer identifies anyone.
public sealed class GetCurrentUserQueryHandler(IUserRepository users) : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse?>
{
    public async Task<CurrentUserResponse?> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(query.UserId, cancellationToken);

        return user is null || user.IsRevoked ? null : new CurrentUserResponse(user.Id, user.DisplayName);
    }
}
