using Microsoft.AspNetCore.Mvc.Testing;

namespace HexMaster.Battleship.Events.Tests.Integration;

public class EventStreamCorsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AllowedOrigin = "http://localhost:4200";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task OpenStream_FromTheFrontendOrigin_AllowsTheOrigin()
    {
        using var response = await OpenStreamFromAsync(AllowedOrigin);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal([AllowedOrigin], values);
    }

    [Fact]
    public async Task OpenStream_FromAnotherOrigin_DoesNotAllowTheOrigin()
    {
        using var response = await OpenStreamFromAsync("http://evil.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_ForAReconnectWithLastEventId_IsAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/events?userId=a");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "last-event-id");

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal([AllowedOrigin], values);
    }

    [Fact]
    public async Task TopicEndpoint_HasNoCorsPolicy()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integration-events")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private async Task<HttpResponseMessage> OpenStreamFromAsync(string origin)
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/events?userId={Guid.NewGuid()}");
        request.Headers.Add("Origin", origin);

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
    }
}
