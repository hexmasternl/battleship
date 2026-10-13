using System.Text;

namespace HexMaster.Battleship.Users.Configuration;

// How the Users service signs tokens and how long they live. Bound from Users:Tokens; the
// shared bearer authentication in ServiceDefaults validates tokens against the same section.
public sealed class TokenOptions
{
    public const string ConfigurationSection = "Users:Tokens";
    public const string SigningKeyConfigurationKey = ConfigurationSection + ":SigningKey";
    public const int MinimumSigningKeyBytes = 32;
    public static readonly TimeSpan MaximumClockSkew = TimeSpan.FromSeconds(30);

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "battleship-users";

    public string Audience { get; set; } = "battleship";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(90);

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    // The key is used as the UTF-8 bytes of the configured string, on both the signing and the
    // validating side.
    public byte[] SigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey);
}
