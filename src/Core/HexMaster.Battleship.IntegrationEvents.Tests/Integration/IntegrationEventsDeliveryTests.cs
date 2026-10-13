using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Dapr.Client;
using HexMaster.Battleship.IntegrationEvents.Players;

namespace HexMaster.Battleship.IntegrationEvents.Tests.Integration;

// End-to-end proof of the integration event path: it starts the real AppHost (the RabbitMQ
// broker, the APIs and their Dapr sidecars), publishes a catalog event through the Players
// sidecar with the production sender, and reads it from the Events API stream of the player it
// targets. Needs Docker and an initialised Dapr runtime, hence the Integration trait.
[Trait("Category", "Integration")]
public class IntegrationEventsDeliveryTests
{
    private const string PubSubName = "pubsub";
    private const string TopicName = "integration-events";
    private const string PlayersApi = "hexmaster-battleship-players-api";
    private const string EventsApi = "hexmaster-battleship-events-api";
    private const string PlayersSidecar = PlayersApi + "-dapr-cli";
    private const string EventsSidecar = EventsApi + "-dapr-cli";

    private static readonly string[] ResourcesThatMustBeHealthy =
    [
        "messaging",
        PlayersApi,
        EventsApi,
        PlayersSidecar,
        EventsSidecar
    ];

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SubscriptionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task PlayerCreated_PublishedByPlayersService_ReachesThePlayersEventStream()
    {
        using var startup = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = startup.Token;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.HexMaster_Battleship_Aspire_AppHost>(cancellationToken);

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        foreach (var resource in ResourcesThatMustBeHealthy)
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(resource, cancellationToken);
        }

        // A healthy sidecar registers its topic subscriptions with the broker a moment after
        // it starts; an event published before that has no queue to land in.
        using var eventsSidecar = app.CreateHttpClient(EventsSidecar, "http");

        var subscribed = await WaitForSubscriptionAsync(eventsSidecar, cancellationToken);

        Assert.True(subscribed, $"The Events sidecar did not register the '{TopicName}' subscription on '{PubSubName}' within {SubscriptionTimeout.TotalSeconds} seconds.");

        var playerId = Guid.NewGuid();

        using var events = app.CreateHttpClient(EventsApi, "http");
        events.Timeout = Timeout.InfiniteTimeSpan;

        using var streamResponse = await events.GetAsync(
            $"/events?playerId={playerId}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        streamResponse.EnsureSuccessStatusCode();

        using var stream = new SseStreamReader(await streamResponse.Content.ReadAsStreamAsync(cancellationToken));

        // The Players service's own sidecar, reached the way a service reaches it: the SDK
        // publishes over gRPC and uses HTTP for everything else.
        using var daprClient = new DaprClientBuilder()
            .UseHttpEndpoint(app.GetEndpoint(PlayersSidecar, "http").ToString())
            .UseGrpcEndpoint(app.GetEndpoint(PlayersSidecar, "grpc").ToString())
            .Build();

        var sender = new DaprIntegrationEventSender(daprClient, TimeProvider.System);
        var created = new PlayerCreatedEvent(playerId, "Captain Nemo");

        await sender.SendAsync(created, EventAudience.Players(playerId), cancellationToken);

        using var delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        delivery.CancelAfter(DeliveryTimeout);

        var message = await ReadNextEventAsync(stream, delivery.Token);

        Assert.Equal(PlayerCreatedEvent.EventType, message.Event);

        using var payload = JsonDocument.Parse(message.Data);
        Assert.Equal(playerId, payload.RootElement.GetProperty("playerId").GetGuid());
        Assert.Equal("Captain Nemo", payload.RootElement.GetProperty("playerName").GetString());
    }

    // Skips heartbeats; the first real event is the one under test.
    private static async Task<SseMessage> ReadNextEventAsync(SseStreamReader stream, CancellationToken cancellationToken)
    {
        while (true)
        {
            var message = await stream.ReadMessageAsync(cancellationToken);

            if (message.Event != "heartbeat")
            {
                return message;
            }
        }
    }

    // Polls the sidecar's metadata API until it lists the integration-events subscription.
    private static async Task<bool> WaitForSubscriptionAsync(HttpClient sidecar, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + SubscriptionTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await sidecar.GetAsync("/v1.0/metadata", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

                if (metadata.RootElement.TryGetProperty("subscriptions", out var subscriptions)
                    && subscriptions.EnumerateArray().Any(IsIntegrationEventsSubscription))
                {
                    return true;
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return false;
    }

    private static bool IsIntegrationEventsSubscription(JsonElement subscription) =>
        subscription.TryGetProperty("pubsubname", out var pubsub) && pubsub.GetString() == PubSubName
        && subscription.TryGetProperty("topic", out var topic) && topic.GetString() == TopicName;
}
