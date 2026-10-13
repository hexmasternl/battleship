using System.ComponentModel.DataAnnotations;

namespace HexMaster.Battleship.Players.Abstractions.DataTransferObjects;

// POST /users/tokens/refresh.
public sealed record RefreshTokensRequest([property: Required] string RefreshToken);
