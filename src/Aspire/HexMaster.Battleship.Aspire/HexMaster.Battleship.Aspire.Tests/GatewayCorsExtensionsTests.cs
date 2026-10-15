using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HexMaster.Battleship.Aspire.Tests;

public sealed class GatewayCorsExtensionsTests
{
    [Fact]
    public async Task AddGatewayCors_InDevelopment_AllowsConfiguredOrigins()
    {
        var policy = await ResolvePolicy(Environments.Development, "http://localhost:4200/, https://localhost:4201");

        Assert.NotNull(policy);
        Assert.Equal(["http://localhost:4200", "https://localhost:4201"], policy.Origins);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
        Assert.False(policy.SupportsCredentials);
    }

    [Fact]
    public async Task AddGatewayCors_InProduction_AllowsConfiguredOrigins()
    {
        var policy = await ResolvePolicy(Environments.Production, "https://battleship.hexmaster.nl");

        Assert.NotNull(policy);
        Assert.Equal(["https://battleship.hexmaster.nl"], policy.Origins);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.SupportsCredentials);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task AddGatewayCors_WithoutOrigins_RegistersNoPolicy(string environment)
    {
        var policy = await ResolvePolicy(environment, allowedOrigins: null);

        Assert.Null(policy);
    }

    [Fact]
    public async Task AddGatewayCors_WithBlankOrigins_RegistersNoPolicy()
    {
        var policy = await ResolvePolicy(Environments.Production, " ; ,");

        Assert.Null(policy);
    }

    [Theory]
    [InlineData("Development", "http://localhost:4200")]
    [InlineData("Production", "https://battleship.hexmaster.nl")]
    public async Task UseGatewayCors_AnswersPreflightFromAllowedOrigin(string environment, string origin)
    {
        await using var app = Build(environment, origin);
        app.UseGatewayCors();
        app.MapGet("/users/x", () => "ok");
        await app.StartAsync();

        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/users/x");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task UseGatewayCors_InProduction_RejectsOtherOrigins()
    {
        await using var app = Build(Environments.Production, "https://battleship.hexmaster.nl");
        app.UseGatewayCors();
        app.MapGet("/users/x", () => "ok");
        await app.StartAsync();

        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/users/x");
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UseGatewayCors_WithoutOrigins_DoesNotThrow()
    {
        await using var app = Build(Environments.Production, allowedOrigins: null);
        app.UseGatewayCors();
        app.MapGet("/users/x", () => "ok");
        await app.StartAsync();

        using var response = await app.GetTestClient().GetAsync("/users/x");

        Assert.True(response.IsSuccessStatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<CorsPolicy?> ResolvePolicy(string environment, string? allowedOrigins)
    {
        await using var app = Build(environment, allowedOrigins);
        var provider = app.Services.GetService<ICorsPolicyProvider>();

        return provider is null
            ? null
            : await provider.GetPolicyAsync(new DefaultHttpContext(), GatewayCorsExtensions.PolicyName);
    }

    private static WebApplication Build(string environment, string? allowedOrigins)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();

        if (allowedOrigins is not null)
        {
            builder.Configuration[GatewayCorsExtensions.AllowedOriginsKey] = allowedOrigins;
        }

        builder.AddGatewayCors();

        return builder.Build();
    }
}
