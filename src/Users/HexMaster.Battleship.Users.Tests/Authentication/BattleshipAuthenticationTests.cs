using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using HexMaster.Battleship.Users.Configuration;
using HexMaster.Battleship.Users.Services;
using HexMaster.Battleship.Users.Tests.Factories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Users.Tests.Authentication;

// A minimal host with only the shared registration, fed tokens from the real JwtTokenService.
public class BattleshipAuthenticationTests
{
    [Fact]
    public async Task TokenFromTheUsersService_IsAccepted_AndExposesSubAndName()
    {
        var options = TokenOptionsFactory.Create();
        var user = UserFactory.Stored(displayName: "Admiral Nelson");
        var token = Issue(options, user, DateTimeOffset.UtcNow);
        await using var app = await StartAsync(options.SigningKey);

        using var response = await SendAsync(app, "/protected", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{user.Id}|Admiral Nelson", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejected()
    {
        var token = Issue(TokenOptionsFactory.Create(new string('x', 32)), UserFactory.Stored(), DateTimeOffset.UtcNow);
        await using var app = await StartAsync(TokenOptionsFactory.SigningKey);

        using var response = await SendAsync(app, "/protected", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_IsRejected()
    {
        var options = TokenOptionsFactory.Create();
        var token = Issue(options, UserFactory.Stored(), DateTimeOffset.UtcNow);
        var parts = token.Split('.');
        var tampered = $"{parts[0]}.{Issue(options, UserFactory.Stored(), DateTimeOffset.UtcNow).Split('.')[1]}.{parts[2]}";
        await using var app = await StartAsync(options.SigningKey);

        using var response = await SendAsync(app, "/protected", tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenExpiredBeyondTheClockSkew_IsRejected()
    {
        var options = TokenOptionsFactory.Create();
        var issuedAt = DateTimeOffset.UtcNow - options.AccessTokenLifetime - options.ClockSkew - TimeSpan.FromSeconds(5);
        var token = Issue(options, UserFactory.Stored(), issuedAt);
        await using var app = await StartAsync(options.SigningKey);

        using var response = await SendAsync(app, "/protected", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfiguredClockSkew_IsApplied()
    {
        var options = TokenOptionsFactory.Create();
        var expiredTenSecondsAgo = DateTimeOffset.UtcNow - options.AccessTokenLifetime - TimeSpan.FromSeconds(10);
        var token = Issue(options, UserFactory.Stored(), expiredTenSecondsAgo);
        await using var strict = await StartAsync(options.SigningKey, clockSkew: "00:00:05");
        await using var lenient = await StartAsync(options.SigningKey, clockSkew: "00:00:30");

        using var rejected = await SendAsync(strict, "/protected", token);
        using var accepted = await SendAsync(lenient, "/protected", token);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task MissingToken_OnAProtectedEndpoint_IsChallengedWithBearer()
    {
        await using var app = await StartAsync(TokenOptionsFactory.SigningKey);

        using var response = await SendAsync(app, "/protected", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task AnonymousEndpoint_StaysReachableWithoutAToken()
    {
        await using var app = await StartAsync(TokenOptionsFactory.SigningKey);

        using var response = await SendAsync(app, "/anonymous", token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "is not configured")]
    [InlineData("too-short-sixteen", "at least 32 bytes")]
    public async Task MissingOrShortSigningKey_FailsStartupNamingTheKey(string? signingKey, string reason)
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => StartAsync(signingKey));

        Assert.Contains("Players:Tokens:SigningKey", exception.Message);
        Assert.Contains(reason, exception.Message);
    }

    private static string Issue(TokenOptions options, Abstractions.DomainModels.IUser user, DateTimeOffset now) =>
        new JwtTokenService(Options.Create(options), new RefreshTokenGenerator()).Issue(user, now).AccessToken;

    private static async Task<WebApplication> StartAsync(string? signingKey, string? clockSkew = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Players:Tokens:SigningKey"] = signingKey,
            ["Players:Tokens:ClockSkew"] = clockSkew,
        });
        builder.AddBattleshipAuthentication();

        var app = builder.Build();
        app.MapGet("/protected", (ClaimsPrincipal user) => $"{user.FindFirstValue("sub")}|{user.Identity?.Name}")
            .RequireAuthorization();
        app.MapGet("/anonymous", () => Results.Ok());

        try
        {
            await app.StartAsync();
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return app;
    }

    private static async Task<HttpResponseMessage> SendAsync(WebApplication app, string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await app.GetTestClient().SendAsync(request);
    }
}
