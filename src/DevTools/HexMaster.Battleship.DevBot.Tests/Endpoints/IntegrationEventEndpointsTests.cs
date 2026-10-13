using System.Net;
using System.Net.Http.Json;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.IntegrationEvents.Games;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Endpoints;

public sealed class IntegrationEventEndpointsTests : IAsyncLifetime
{
    private readonly DevBotApplicationFactory factory = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task DaprSubscribe_ListsTheIntegrationEventsTopic()
    {
        using var client = factory.CreateClient();

        using var body = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/dapr/subscribe"));

        var subscription = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("pubsub", subscription.GetProperty("pubsubName").GetString());
        Assert.Equal("integration-events", subscription.GetProperty("topic").GetString());
        Assert.Equal("integration-events", subscription.GetProperty("route").GetString()!.TrimStart('/'));
    }

    [Fact]
    public async Task Receive_ValidEnvelope_IsAcknowledged()
    {
        using var client = factory.CreateClient();
        var envelope = EnvelopeFactory.Create(
            new GameCreatedEvent(Guid.NewGuid(), GameViewFactory.Code(), Guid.NewGuid()), DateTimeOffset.UtcNow, Guid.NewGuid());

        using var response = await client.PostAsJsonAsync("/integration-events", envelope);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"id":"","type":"","target":"game","targetIds":[],"occurredAt":"2026-10-13T12:00:00Z","payload":{}}""")]
    public async Task Receive_InvalidEnvelope_IsAcknowledgedAndDropped(string body)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/integration-events", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.Users.Verify(u => u.CreateTokensAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Receive_HandlerFails_IsStillAcknowledged()
    {
        factory.Users.Setup(u => u.CreateTokensAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("users down"));
        using var client = factory.CreateClient();
        var envelope = EnvelopeFactory.Create(
            new GameCreatedEvent(Guid.NewGuid(), GameViewFactory.Code(), Guid.NewGuid()), DateTimeOffset.UtcNow, Guid.NewGuid());

        using var response = await client.PostAsJsonAsync("/integration-events", envelope);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
