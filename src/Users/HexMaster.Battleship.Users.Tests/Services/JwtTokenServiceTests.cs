using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Users.Configuration;
using HexMaster.Battleship.Users.Services;
using HexMaster.Battleship.Users.Tests.Factories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HexMaster.Battleship.Users.Tests.Services;

public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, 250, TimeSpan.Zero);
    private static readonly DateTimeOffset NowInSeconds = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_SignsAnAccessTokenCarryingEveryClaim()
    {
        var options = TokenOptionsFactory.Create();
        var user = UserFactory.Stored(displayName: "Admiral Nelson");

        var issued = CreateService(options).Issue(user, Now);

        var token = new JsonWebToken(issued.AccessToken);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Alg);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal("Admiral Nelson", token.GetClaim(JwtRegisteredClaimNames.Name).Value);
        Assert.True(Guid.TryParse(token.Id, out _));
        Assert.Equal(options.Issuer, token.Issuer);
        Assert.Equal([options.Audience], token.Audiences);
        Assert.Equal(NowInSeconds.UtcDateTime, token.IssuedAt);
    }

    [Fact]
    public void Issue_ExpiresTheAccessTokenAfterTheConfiguredLifetime()
    {
        var options = TokenOptionsFactory.Create();
        options.AccessTokenLifetime = TimeSpan.FromMinutes(5);

        var issued = CreateService(options).Issue(UserFactory.Stored(), Now);

        var token = new JsonWebToken(issued.AccessToken);
        Assert.Equal(NowInSeconds.AddMinutes(5), issued.AccessTokenExpiresAt);
        Assert.Equal(token.IssuedAt.AddMinutes(5), token.ValidTo);
        Assert.Equal(issued.AccessTokenExpiresAt.UtcDateTime, token.ValidTo);
    }

    [Fact]
    public async Task Issue_ProducesATokenThatValidatesWithTheSigningKey()
    {
        var options = TokenOptionsFactory.Create();
        var issued = CreateService(options).Issue(UserFactory.Stored(), DateTimeOffset.UtcNow);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(options.SigningKeyBytes()),
        });

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public void Issue_CreatesANewRefreshTokenStoredOnlyAsItsHash()
    {
        var options = TokenOptionsFactory.Create();
        var user = UserFactory.Stored();

        var issued = CreateService(options).Issue(user, Now);

        Assert.NotEqual(issued.RefreshToken, issued.StoredRefreshToken.Id);
        Assert.Equal(RefreshTokenGenerator.Hash(issued.RefreshToken), issued.StoredRefreshToken.Id);
        Assert.Equal(user.Id, issued.StoredRefreshToken.UserId);
        Assert.Equal(NowInSeconds, issued.StoredRefreshToken.IssuedAt);
        Assert.Equal(NowInSeconds.AddDays(90), issued.StoredRefreshToken.ExpiresAt);
        Assert.Equal(DomainModelState.New, issued.StoredRefreshToken.State);
    }

    [Fact]
    public void Issue_GivesEveryPairADifferentRefreshTokenAndTokenId()
    {
        var service = CreateService(TokenOptionsFactory.Create());
        var user = UserFactory.Stored();

        var first = service.Issue(user, Now);
        var second = service.Issue(user, Now);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(new JsonWebToken(first.AccessToken).Id, new JsonWebToken(second.AccessToken).Id);
    }

    [Fact]
    public void ToResponse_CarriesBothTokensAndExpiries()
    {
        var issued = CreateService(TokenOptionsFactory.Create()).Issue(UserFactory.Stored(), Now);

        var response = issued.ToResponse();

        Assert.Equal(issued.AccessToken, response.AccessToken);
        Assert.Equal(issued.AccessTokenExpiresAt, response.AccessTokenExpiresAt);
        Assert.Equal(issued.RefreshToken, response.RefreshToken);
        Assert.Equal(issued.StoredRefreshToken.ExpiresAt, response.RefreshTokenExpiresAt);
        Assert.True(response.RefreshTokenExpiresAt > response.AccessTokenExpiresAt);
    }

    private static JwtTokenService CreateService(TokenOptions options) =>
        new(Options.Create(options), new RefreshTokenGenerator());
}
