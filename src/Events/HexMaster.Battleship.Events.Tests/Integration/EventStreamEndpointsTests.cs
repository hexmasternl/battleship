using System.Net;
using HexMaster.Battleship.Events.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.Battleship.Events.Tests.Integration;

public class EventStreamEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task OpenStream_WithUserId_SendsEventStreamHeadersAndStaysOpen()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid().ToString();

        using var response = await client.GetAsync(
            $"/events?userId={userId}", HttpCompletionOption.ResponseHeadersRead, cancellation.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // The headers arrived while the stream is still registered, so it is open and unbuffered.
        var registry = factory.Services.GetRequiredService<IConnectionRegistry>();
        Assert.Single(registry.ConnectionsFor(userId));
    }

    [Fact]
    public async Task OpenStream_ClientDisconnects_ConnectionIsRemoved()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid().ToString();
        var registry = factory.Services.GetRequiredService<IConnectionRegistry>();

        var response = await client.GetAsync(
            $"/events?userId={userId}", HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        Assert.Single(registry.ConnectionsFor(userId));

        response.Dispose();

        while (registry.ConnectionsFor(userId).Count > 0)
        {
            await Task.Delay(20, cancellation.Token);
        }
    }

    [Theory]
    [InlineData("/events")]
    [InlineData("/events?userId=")]
    [InlineData("/events?userId=%20%20")]
    [InlineData("/events?playerId=0b7d2c4e-6f1a-4c3b-9e8d-2a5f7c9e1b3d")]
    public async Task OpenStream_WithoutUserId_Returns400ProblemDetails(string url)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
