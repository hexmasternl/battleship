using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HexMaster.Battleship.Games.Tests.Integration;

// Access tokens shaped like the ones the Players service issues, signed with a test key.
public static class TestTokens
{
    // 32 ASCII characters, so exactly the minimum key size.
    public const string SigningKey = "battleship-test-signing-key-0032";

    public static string Issue(
        Guid playerId,
        string? playerName,
        DateTimeOffset? expiresAt = null,
        string signingKey = SigningKey)
    {
        var expires = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(15);
        var claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = playerId.ToString() };
        if (playerName is not null)
        {
            claims[JwtRegisteredClaimNames.Name] = playerName;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "battleship-players",
            Audience = "battleship",
            Claims = claims,
            IssuedAt = expires.AddMinutes(-15).UtcDateTime,
            NotBefore = expires.AddMinutes(-15).UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}
