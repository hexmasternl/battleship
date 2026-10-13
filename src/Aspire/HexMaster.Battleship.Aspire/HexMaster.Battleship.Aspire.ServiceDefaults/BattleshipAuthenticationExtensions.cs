using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

// Validates the access tokens the Users service issues, for any Battleship API. Reads the same
// Users:Tokens section the Users service signs with, and stops the host at startup when the
// signing key is missing or too short. Endpoints opt in with RequireAuthorization(); nothing is
// protected by this registration alone. WebApplication adds the authentication and authorization
// middleware itself once these services exist.
public static class BattleshipAuthenticationExtensions
{
    public static TBuilder AddBattleshipAuthentication<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<BearerTokenValidationOptions>()
            .BindConfiguration(BearerTokenValidationOptions.ConfigurationSection)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<BearerTokenValidationOptions>, BearerTokenValidationOptionsValidator>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<BearerTokenValidationOptions>>((bearer, validation) =>
            {
                var settings = validation.Value;

                // Keep the claim names as issued: sub is the user id, name the display name.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(settings.SigningKeyBytes()),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = settings.ClockSkew,
                    NameClaimType = JwtRegisteredClaimNames.Name,
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }
}

// The validating side of Users:Tokens. Defaults match the Users service's token options.
public sealed class BearerTokenValidationOptions
{
    public const string ConfigurationSection = "Users:Tokens";
    public const string SigningKeyConfigurationKey = ConfigurationSection + ":SigningKey";
    public const int MinimumSigningKeyBytes = 32;
    public static readonly TimeSpan MaximumClockSkew = TimeSpan.FromSeconds(30);

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "battleship-users";

    public string Audience { get; set; } = "battleship";

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    public byte[] SigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey);
}

public sealed class BearerTokenValidationOptionsValidator : IValidateOptions<BearerTokenValidationOptions>
{
    public ValidateOptionsResult Validate(string? name, BearerTokenValidationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return ValidateOptionsResult.Fail($"{BearerTokenValidationOptions.SigningKeyConfigurationKey} is not configured.");
        }

        if (options.SigningKeyBytes().Length < BearerTokenValidationOptions.MinimumSigningKeyBytes)
        {
            return ValidateOptionsResult.Fail(
                $"{BearerTokenValidationOptions.SigningKeyConfigurationKey} must be at least {BearerTokenValidationOptions.MinimumSigningKeyBytes} bytes.");
        }

        if (options.ClockSkew < TimeSpan.Zero || options.ClockSkew > BearerTokenValidationOptions.MaximumClockSkew)
        {
            return ValidateOptionsResult.Fail(
                $"{BearerTokenValidationOptions.ConfigurationSection}:ClockSkew must be between 0 and {BearerTokenValidationOptions.MaximumClockSkew}.");
        }

        return ValidateOptionsResult.Success;
    }
}
