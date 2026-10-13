namespace HexMaster.Battleship.Players.Abstractions.DataTransferObjects;

// GET /users/me.
public sealed record CurrentUserResponse(Guid Id, string DisplayName);
