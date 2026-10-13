using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace HexMaster.Battleship.Games.Tests.Integration;

// End-to-end smoke test of the local messaging backbone: it starts the real AppHost (the
// RabbitMQ broker, both APIs and their Dapr sidecars), publishes an event from the Players
// API and waits for it to arrive at the Games API through the shared pubsub component.
// Needs Docker and an initialised Dapr runtime, hence the Integration trait.
[Trait("Category", "Integration")]
public class SmokeMessagingTests
{
    private const string PubSubName = "pubsub";
    private const string TopicName = "smoke";
    private const string PlayersApi = "hexmaster-battleship-players-api";
    private const string GamesApi = "hexmaster-battleship-games-api";
    private const string GamesSidecar = GamesApi + "-dapr-cli";
    private const string PlayersSidecar = PlayersApi + "-dapr-cli";

    private static readonly string[] ResourcesThatMustBeHealthy =
    [
        "messaging",
        PlayersApi,
        GamesApi,
        PlayersSidecar,
        GamesSidecar
    ];

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SubscriptionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task SmokeEvent_PublishedByPlayersApi_IsReceivedByGamesApi()
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
        using var gamesSidecar = app.CreateHttpClient(GamesSidecar, "http");

        var subscribed = await WaitForSubscriptionAsync(gamesSidecar, cancellationToken);

        Assert.True(subscribed, $"The Games sidecar did not register the '{TopicName}' subscription on '{PubSubName}' within {SubscriptionTimeout.TotalSeconds} seconds.");

        using var players = app.CreateHttpClient(PlayersApi, "http");
        using var games = app.CreateHttpClient(GamesApi, "http");

        using var published = await players.PostAsync("/smoke/publish", content: null, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, published.StatusCode);

        var smokeEvent = await published.Content.ReadFromJsonAsync<SmokeEvent>(cancellationToken);

        Assert.NotNull(smokeEvent);
        Assert.NotEqual(Guid.Empty, smokeEvent.Id);

        var received = await WaitForReceivedAsync(games, smokeEvent.Id, cancellationToken);

        Assert.True(received, $"Smoke event {smokeEvent.Id} did not reach the Games API within {DeliveryTimeout.TotalSeconds} seconds.");
    }

    // Polls the sidecar's metadata API until it lists the smoke topic subscription.
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
                    && subscriptions.EnumerateArray().Any(IsSmokeSubscription))
                {
                    return true;
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return false;
    }

    private static bool IsSmokeSubscription(JsonElement subscription) =>
        subscription.TryGetProperty("pubsubname", out var pubsub) && pubsub.GetString() == PubSubName
        && subscription.TryGetProperty("topic", out var topic) && topic.GetString() == TopicName;

    private static async Task<bool> WaitForReceivedAsync(HttpClient games, Guid id, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + DeliveryTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await games.GetAsync($"/smoke/received/{id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return false;
    }

    private sealed record SmokeEvent(Guid Id);
}
