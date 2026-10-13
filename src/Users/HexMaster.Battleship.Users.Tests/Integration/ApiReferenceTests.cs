using System.Net;
using HexMaster.Battleship.Users.Abstractions.Repositories;
using HexMaster.Battleship.Users.Tests.Factories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HexMaster.Battleship.Users.Tests.Integration;

// The OpenAPI document and the Scalar reference exist in Development and nowhere else.
// Needs no storage: the repositories are mocked.
public class ApiReferenceTests
{
    public static TheoryData<string> Paths => ["/openapi/v1.json", "/scalar", "/scalar/v1"];

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Development_ServesTheReference(string path)
    {
        await using var factory = new Factory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Production_DoesNotServeTheReference(string path)
    {
        await using var factory = new Factory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class Factory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Players:Tokens:SigningKey", TokenOptionsFactory.SigningKey);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(Mock.Of<IUserRepository>());
                services.AddSingleton(Mock.Of<IRefreshTokenRepository>());
            });
        }
    }
}
