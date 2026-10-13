namespace HexMaster.Battleship.Players.Features.RefreshTokens;

// Exchanges a refresh token for a new pair; the presented token can never be used again.
public sealed record RefreshTokensCommand(string RefreshToken);
