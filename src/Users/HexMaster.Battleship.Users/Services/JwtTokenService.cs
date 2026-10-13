using HexMaster.Battleship.Users.Abstractions.DomainModels;
using HexMaster.Battleship.Users.Configuration;
using HexMaster.Battleship.Users.DomainModels;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HexMaster.Battleship.Users.Services;

// Signs HS256 access tokens carrying the user id as sub and the display name as name, and
// pairs each with a fresh refresh token.
public sealed class JwtTokenService(IOptions<TokenOptions> options, RefreshTokenGenerator refreshTokens) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedTokens Issue(IUser user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);
        var settings = options.Value;

        // JWT times are whole seconds; truncating keeps the returned expiry equal to the exp claim.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var accessTokenExpiresAt = issuedAt + settings.AccessTokenLifetime;

        var accessToken = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = accessTokenExpiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Name] = user.DisplayName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(settings.SigningKeyBytes()), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = refreshTokens.Generate();
        var storedRefreshToken = RefreshToken.Create(
            RefreshTokenGenerator.Hash(refreshToken), user.Id, issuedAt, settings.RefreshTokenLifetime);

        return new IssuedTokens(accessToken, accessTokenExpiresAt, refreshToken, storedRefreshToken);
    }
}
