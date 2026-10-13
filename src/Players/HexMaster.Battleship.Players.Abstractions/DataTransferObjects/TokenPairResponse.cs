namespace HexMaster.Battleship.Players.Abstractions.DataTransferObjects;

// The tokens returned by POST /tokens and POST /tokens/refresh. The refresh token is returned
// once and never stored by the service in this form.
public sealed record TokenPairResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
