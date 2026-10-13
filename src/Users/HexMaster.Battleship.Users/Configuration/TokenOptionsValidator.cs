using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Users.Configuration;

// Runs on start, so the Users API refuses to start without a usable signing key.
public sealed class TokenOptionsValidator : IValidateOptions<TokenOptions>
{
    public ValidateOptionsResult Validate(string? name, TokenOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add($"{TokenOptions.SigningKeyConfigurationKey} is not configured.");
        }
        else if (options.SigningKeyBytes().Length < TokenOptions.MinimumSigningKeyBytes)
        {
            failures.Add(
                $"{TokenOptions.SigningKeyConfigurationKey} must be at least {TokenOptions.MinimumSigningKeyBytes} bytes.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"{TokenOptions.ConfigurationSection}:Issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{TokenOptions.ConfigurationSection}:Audience is not configured.");
        }

        if (options.AccessTokenLifetime <= TimeSpan.Zero)
        {
            failures.Add($"{TokenOptions.ConfigurationSection}:AccessTokenLifetime must be positive.");
        }

        if (options.RefreshTokenLifetime <= options.AccessTokenLifetime)
        {
            failures.Add($"{TokenOptions.ConfigurationSection}:RefreshTokenLifetime must be longer than the access token lifetime.");
        }

        if (options.ClockSkew < TimeSpan.Zero || options.ClockSkew > TokenOptions.MaximumClockSkew)
        {
            failures.Add($"{TokenOptions.ConfigurationSection}:ClockSkew must be between 0 and {TokenOptions.MaximumClockSkew}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
