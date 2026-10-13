using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Observability;
using HexMaster.Battleship.Events.Tests.Factories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

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
    public async Task InvalidEnvelope_IsCountedAsDropped()
    {
        using var client = factory.CreateClient();
        using var dropped = new MetricCollector<long>(
            factory.Services.GetRequiredService<IMeterFactory>(), EventsTelemetry.Name, EventsMetrics.DroppedName);

        using var response = await client.PostAsJsonAsync(
            "/integration-events", IntegrationEventEnvelopeFactory.Create() with { TargetIds = [] }, JsonSerializerOptions.Web);

        await AssertDroppedAsync(response);
        var measurement = Assert.Single(dropped.GetMeasurementSnapshot());
        Assert.Equal(DropReasons.InvalidEnvelope, measurement.Tags[EventsTelemetry.ReasonTag]);
    }

    [Fact]
    public async Task ValidEnvelope_IsProcessedInAConsumerSpanWithTheDeliverSpanBeneathIt()
    {
        using var client = factory.CreateClient();
        var envelope = IntegrationEventEnvelopeFactory.ForPlayer(Guid.NewGuid().ToString(), "shot-fired");
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == EventsTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);

        await PostAsync(client, envelope);

        var process = Assert.Single(stopped, a => (string?)a.GetTagItem(EventsTelemetry.MessageIdAttribute) == envelope.Id);
        Assert.Equal("process integration-events", process.OperationName);
        Assert.Equal(ActivityKind.Consumer, process.Kind);
        Assert.Equal(ActivityStatusCode.Ok, process.Status);
        Assert.Equal("dapr", process.GetTagItem(EventsTelemetry.MessagingSystemAttribute));
        Assert.Equal("process", process.GetTagItem(EventsTelemetry.OperationTypeAttribute));
        Assert.Equal("integration-events", process.GetTagItem(EventsTelemetry.DestinationNameAttribute));
        Assert.Equal("shot-fired", process.GetTagItem(EventsTelemetry.EventTypeAttribute));

        var deliver = Assert.Single(stopped, a => a.ParentSpanId == process.SpanId);
        Assert.Equal(EventsTelemetry.DeliverActivityName, deliver.OperationName);
        Assert.Equal(process.TraceId, deliver.TraceId);
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
