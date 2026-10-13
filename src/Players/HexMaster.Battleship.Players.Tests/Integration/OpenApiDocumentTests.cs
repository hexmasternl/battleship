using System.Text.Json;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Tests.Factories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HexMaster.Battleship.Players.Tests.Integration;

// Needs no storage: the repositories are mocked, only the endpoint metadata is inspected.
public class OpenApiDocumentTests : IClassFixture<OpenApiDocumentTests.Factory>
{
    private readonly Factory _factory;

    public OpenApiDocumentTests(Factory factory) => _factory = factory;

    [Theory]
    [InlineData("/tokens", "post", "201")]
    [InlineData("/tokens/refresh", "post", "200")]
    [InlineData("/tokens/refresh", "post", "401")]
    [InlineData("/users/me", "get", "200")]
    public async Task Document_ListsThePlayersEndpoints(string path, string method, string status)
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
