using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Tests.Factories;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HexMaster.Battleship.Events.Tests.Integration;

public class IntegrationEventEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task PlayerEnvelope_PostedToTopicEndpoint_ArrivesOnThePlayersStreamWithinOneSecond()
    {
        using var client = factory.CreateClient();
        var playerA = Guid.NewGuid().ToString();
        var payload = JsonSerializer.SerializeToElement(new { x = 3, y = 5 });
        var envelope = IntegrationEventEnvelopeFactory.Create(targetIds: [playerA], type: "shot-fired", payload: payload);

        using var stream = await OpenStreamAsync(client, playerA);

        using var posted = await client.PostAsJsonAsync("/integration-events", envelope, JsonSerializerOptions.Web);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);

        using var delivery = new CancellationTokenSource(DeliveryTimeout);
        var message = await stream.ReadMessageAsync(delivery.Token);

        Assert.True(long.TryParse(message.Id, out var sequence) && sequence > 0);
        Assert.Equal("shot-fired", message.Event);
        Assert.Equal(payload.GetRawText(), message.Data);
    }

    [Fact]
    public async Task EnvelopeForAnotherPlayer_IsNotReceived()
    {
        using var client = factory.CreateClient();
        var playerA = Guid.NewGuid().ToString();
        var playerB = Guid.NewGuid().ToString();

        using var stream = await OpenStreamAsync(client, playerA);

        await PostAsync(client, IntegrationEventEnvelopeFactory.ForPlayer(playerB, "for-b"));
        await PostAsync(client, IntegrationEventEnvelopeFactory.ForPlayer(playerA, "for-a"));

        using var delivery = new CancellationTokenSource(DeliveryTimeout);
        var message = await stream.ReadMessageAsync(delivery.Token);

        Assert.Equal("for-a", message.Event);
    }

    [Fact]
    public async Task EnvelopeWithoutMatchingConnection_IsAcknowledged()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/integration-events",
            IntegrationEventEnvelopeFactory.ForPlayer(Guid.NewGuid().ToString()),
            JsonSerializerOptions.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task InvalidEnvelope_IsAcknowledgedWithDropSoDaprDoesNotRedeliver()
    {
        using var client = factory.CreateClient();
        var envelope = IntegrationEventEnvelopeFactory.Create() with { TargetIds = [] };

        using var response = await client.PostAsJsonAsync("/integration-events", envelope, JsonSerializerOptions.Web);

        await AssertDroppedAsync(response);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"id":"1","type":"t","target":"team","targetIds":[],"payload":{}}""")]
    public async Task UnparsableBody_IsAcknowledgedWithDrop(string body)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/integration-events", new StringContent(body, Encoding.UTF8, "application/json"));

        await AssertDroppedAsync(response);
    }

    [Fact]
    public async Task SubscribeHandler_ListsTheIntegrationEventsTopic()
    {
        using var client = factory.CreateClient();

        using var subscriptions = JsonDocument.Parse(await client.GetStringAsync("/dapr/subscribe"));

        Assert.Contains(
            subscriptions.RootElement.EnumerateArray(),
            s => s.GetProperty("pubsubName").GetString() == "pubsub"
                 && s.GetProperty("topic").GetString() == "integration-events"
                 && s.GetProperty("route").GetString() == "integration-events");
    }

    private static async Task<SseStreamReader> OpenStreamAsync(HttpClient client, string playerId)
    {
        using var connect = new CancellationTokenSource(ConnectTimeout);
        var response = await client.GetAsync(
            $"/events?playerId={playerId}", HttpCompletionOption.ResponseHeadersRead, connect.Token);
        response.EnsureSuccessStatusCode();

        return new SseStreamReader(await response.Content.ReadAsStreamAsync(connect.Token));
    }

    private static async Task PostAsync(HttpClient client, IntegrationEventEnvelope envelope)
    {
        using var response = await client.PostAsJsonAsync("/integration-events", envelope, JsonSerializerOptions.Web);
        response.EnsureSuccessStatusCode();
    }

    private static async Task AssertDroppedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("DROP", body.RootElement.GetProperty("status").GetString());
    }
}
