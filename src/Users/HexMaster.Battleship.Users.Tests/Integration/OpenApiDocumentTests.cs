using System.Text.Json;
using HexMaster.Battleship.Users.Abstractions.Repositories;
using HexMaster.Battleship.Users.Tests.Factories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HexMaster.Battleship.Users.Tests.Integration;

// Needs no storage: the repositories are mocked, only the endpoint metadata is inspected.
public class OpenApiDocumentTests : IClassFixture<OpenApiDocumentTests.Factory>
{
    private readonly Factory _factory;

    public OpenApiDocumentTests(Factory factory) => _factory = factory;

    [Theory]
    [InlineData("/users/tokens", "post", "201")]
    [InlineData("/users/tokens/refresh", "post", "200")]
    [InlineData("/users/tokens/refresh", "post", "401")]
    [InlineData("/users/me", "get", "200")]
    public async Task Document_ListsTheUsersEndpoints(string path, string method, string status)
    {
        using var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
    }

    [Fact]
    public async Task Document_NoLongerListsTheWeatherForecast()
    {
        using var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty("/weatherforecast", out _));
    }

    [Fact]
    public async Task Document_OmitsTheDaprSubscribeRoute()
    {
        using var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty("/dapr/subscribe", out _));
    }

    [Fact]
    public async Task Document_DescribesTheBearerScheme()
    {
        using var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
    }

    [Theory]
    [InlineData("/users/me", "get", true)]
    [InlineData("/users/tokens", "post", false)]
    [InlineData("/users/tokens/refresh", "post", false)]
    public async Task Document_MarksOnlyProtectedOperationsAsSecured(string path, string method, bool secured)
    {
        using var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        var requiresBearer = operation.TryGetProperty("security", out var security)
            && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));
        Assert.Equal(secured, requiresBearer);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Players:Tokens:SigningKey", TokenOptionsFactory.SigningKey);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(Mock.Of<IUserRepository>());
                services.AddSingleton(Mock.Of<IRefreshTokenRepository>());
            });
        }
    }
}
