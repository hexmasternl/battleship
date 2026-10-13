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
    public async Task AddGatewayCors_InDevelopmentWithoutOrigins_AllowsNoOrigin()
    {
        var policy = await ResolvePolicy(Environments.Development, allowedOrigins: null);

        Assert.NotNull(policy);
        Assert.Empty(policy.Origins);
        Assert.False(policy.AllowAnyOrigin);
    }

    [Fact]
    public async Task AddGatewayCors_OutsideDevelopment_RegistersNoPolicy()
    {
        var policy = await ResolvePolicy(Environments.Production, "http://localhost:4200");

        Assert.Null(policy);
    }

    [Fact]
    public async Task UseGatewayCors_AnswersPreflightFromAllowedOrigin()
    {
        await using var app = Build(Environments.Development, "http://localhost:4200");
        app.UseGatewayCors();
        app.MapGet("/users/x", () => "ok");
        await app.StartAsync();

        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/users/x");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.Equal("http://localhost:4200", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
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
