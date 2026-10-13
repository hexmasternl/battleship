using System.ComponentModel.DataAnnotations;

namespace HexMaster.Battleship.Users.Abstractions.DataTransferObjects;

// POST /users/tokens/refresh.
public sealed record RefreshTokensRequest([property: Required] string RefreshToken);
