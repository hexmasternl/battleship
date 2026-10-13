using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace HexMaster.Battleship.Players.Services;

// Refresh tokens are 256 random bits, Base64Url encoded. Only their hash is ever stored.
public sealed class RefreshTokenGenerator
{
    public const int TokenBytes = 32;

    public string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    // The Base64Url SHA-256 hash of a token value: the key under which the token is stored.
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
