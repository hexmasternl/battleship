using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Users.Abstractions.Repositories;
using HexMaster.Battleship.Users.Observability;

namespace HexMaster.Battleship.Users.Features.GetCurrentUser;

// Returns null when the user no longer exists or was revoked, so the token no longer identifies anyone.
public sealed class GetCurrentUserQueryHandler(IUserRepository users) : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse?>
{
    public async Task<CurrentUserResponse?> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        Activity.Current?.SetTag(UsersTelemetry.UserIdAttribute, query.UserId.ToString());

        var user = await users.GetAsync(query.UserId, cancellationToken);

        return user is null || user.IsRevoked ? null : new CurrentUserResponse(user.Id, user.DisplayName);
    }
}
