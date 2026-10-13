using HexMaster.Battleship.Users.Configuration;
using HexMaster.Battleship.Users.Tests.Factories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Users.Tests.Configuration;

public class TokenOptionsTests
{
    private readonly TokenOptionsValidator _validator = new();

    [Fact]
    public void Defaults_MatchTheDesign()
    {
        var options = new TokenOptions();

        Assert.Equal("battleship-users", options.Issuer);
        Assert.Equal("battleship", options.Audience);
        Assert.Equal(TimeSpan.FromMinutes(15), options.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(90), options.RefreshTokenLifetime);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ClockSkew);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSigningKey_FailsNamingTheConfigurationKey(string signingKey)
    {
        var result = _validator.Validate(null, TokenOptionsFactory.Create(signingKey));

        Assert.True(result.Failed);
        Assert.Contains("Users:Tokens:SigningKey", result.FailureMessage);
    }

    [Fact]
    public void SixteenByteSigningKey_Fails()
    {
        var result = _validator.Validate(null, TokenOptionsFactory.Create(new string('k', 16)));

        Assert.True(result.Failed);
        Assert.Contains("at least 32 bytes", result.FailureMessage);
    }

    [Fact]
    public void ThirtyTwoByteSigningKey_Passes()
    {
        var result = _validator.Validate(null, TokenOptionsFactory.Create(new string('k', 32)));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ClockSkewAboveThirtySeconds_Fails()
    {
        var options = TokenOptionsFactory.Create();
        options.ClockSkew = TimeSpan.FromSeconds(31);

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void NonPositiveAccessTokenLifetime_Fails()
    {
        var options = TokenOptionsFactory.Create();
        options.AccessTokenLifetime = TimeSpan.Zero;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void RefreshLifetimeNotLongerThanAccessLifetime_Fails()
    {
        var options = TokenOptionsFactory.Create();
        options.RefreshTokenLifetime = options.AccessTokenLifetime;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void BlankIssuerAndAudience_Fail()
    {
        var options = TokenOptionsFactory.Create();
        options.Issuer = " ";
        options.Audience = "";

        var result = _validator.Validate(null, options);

        Assert.Contains("Issuer", result.FailureMessage);
        Assert.Contains("Audience", result.FailureMessage);
    }

    [Fact]
    public void AddUsersModule_BindsTheSectionAndValidates()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Users:Tokens:SigningKey"] = new string('k', 16),
                ["Users:Tokens:AccessTokenLifetime"] = "00:05:00",
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddUsersModule()
            .BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IOptions<TokenOptions>>().Value);
        Assert.Contains("Users:Tokens:SigningKey", exception.Message);
    }
}
