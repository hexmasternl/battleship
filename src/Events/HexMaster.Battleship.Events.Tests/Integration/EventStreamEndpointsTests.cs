using System.Net;
using HexMaster.Battleship.Events.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.Battleship.Events.Tests.Integration;

public class EventStreamEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task OpenStream_WithPlayerId_SendsEventStreamHeadersAndStaysOpen()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        using var client = factory.CreateClient();
        var playerId = Guid.NewGuid().ToString();

        using var response = await client.GetAsync(
            $"/events?playerId={playerId}", HttpCompletionOption.ResponseHeadersRead, cancellation.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // The headers arrived while the stream is still registered, so it is open and unbuffered.
        var registry = factory.Services.GetRequiredService<IConnectionRegistry>();
        Assert.Single(registry.ConnectionsFor(playerId));
    }

    [Fact]
    public async Task OpenStream_ClientDisconnects_ConnectionIsRemoved()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        using var client = factory.CreateClient();
        var playerId = Guid.NewGuid().ToString();
        var registry = factory.Services.GetRequiredService<IConnectionRegistry>();

        var response = await client.GetAsync(
            $"/events?playerId={playerId}", HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        Assert.Single(registry.ConnectionsFor(playerId));

        response.Dispose();

        while (registry.ConnectionsFor(playerId).Count > 0)
        {
            await Task.Delay(20, cancellation.Token);
        }
    }

    [Theory]
    [InlineData("/events")]
    [InlineData("/events?playerId=")]
    [InlineData("/events?playerId=%20%20")]
    public async Task OpenStream_WithoutPlayerId_Returns400ProblemDetails(string url)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
