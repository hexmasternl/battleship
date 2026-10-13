namespace HexMaster.Battleship.Players.Features.GetCurrentUser;

// Reads the identity of the user an access token belongs to.
public sealed record GetCurrentUserQuery(Guid UserId);
