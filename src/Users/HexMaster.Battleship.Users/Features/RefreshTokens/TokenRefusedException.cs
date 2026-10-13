namespace HexMaster.Battleship.Users.Features.RefreshTokens;

// A refresh token that does not lead to a new pair. The endpoint answers 401 with problem details
// and never tells the client which reason applied.
public sealed class TokenRefusedException(string reason) : Exception($"The refresh token was refused: {reason}")
{
    public string Reason { get; } = reason;
}
