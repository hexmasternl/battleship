using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dapr.Client;
using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.IntegrationEvents.Observability;
using HexMaster.Battleship.IntegrationEvents.Tests.Factories;
using HexMaster.Battleship.IntegrationEvents.Tests.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.IntegrationEvents.Tests.Sending;

public class DaprIntegrationEventSenderTests
{
    private readonly Mock<DaprClient> _daprClient = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 13, 9, 30, 0, TimeSpan.Zero));
    private readonly List<IntegrationEventEnvelope> _published = [];
    private readonly IMeterFactory _meterFactory =
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();

    public DaprIntegrationEventSenderTests()
    {
        _daprClient
            .Setup(c => c.PublishEventAsync(
                "pubsub", "integration-events", It.IsAny<IntegrationEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, IntegrationEventEnvelope, CancellationToken>((_, _, envelope, _) => _published.Add(envelope))
            .Returns(Task.CompletedTask);
    }

    private DaprIntegrationEventSender CreateSender() => new(
        _daprClient.Object,
        _timeProvider,
        new IntegrationEventsMetrics(_meterFactory),
        NullLogger<DaprIntegrationEventSender>.Instance);

    [Fact]
    public async Task SendAsync_PublishesTheEnvelopeToIntegrationEventsOnPubSub()
    {
        var playerId = Guid.NewGuid();
        var created = IntegrationEventFactory.PlayerCreated(playerId);

        await CreateSender().SendAsync(created, EventAudience.Players(playerId));

        var envelope = Assert.Single(_published);
        Assert.True(Guid.TryParse(envelope.Id, out _));
        Assert.Equal("player.created.v1", envelope.Type);
        Assert.Equal(EventTarget.Player, envelope.Target);
        Assert.Equal([playerId.ToString()], envelope.TargetIds);
        Assert.Equal(_timeProvider.GetUtcNow(), envelope.OccurredAt);
        Assert.Equal(TimeSpan.Zero, envelope.OccurredAt.Offset);
        Assert.Equal(playerId, envelope.Payload.GetProperty("playerId").GetGuid());
        Assert.Equal(created.PlayerName, envelope.Payload.GetProperty("playerName").GetString());
    }

    [Fact]
    public async Task SendAsync_GameStarted_CarriesItsTypeName()
    {
        await CreateSender().SendAsync(IntegrationEventFactory.GameStarted(), EventAudience.Everyone);

        Assert.Equal("game.started.v1", Assert.Single(_published).Type);
    }

    [Fact]
    public async Task SendAsync_GameAudience_TargetsTheGameAndItsPlayers()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await CreateSender().SendAsync(IntegrationEventFactory.GameJoined(), EventAudience.Game(a, b));

        var envelope = Assert.Single(_published);
        Assert.Equal(EventTarget.Game, envelope.Target);
        Assert.Equal([a.ToString(), b.ToString()], envelope.TargetIds);
    }

    [Fact]
    public async Task SendAsync_Everyone_TargetsAllWithoutIds()
    {
        await CreateSender().SendAsync(IntegrationEventFactory.PlayerUpdated(), EventAudience.Everyone);

        var envelope = Assert.Single(_published);
        Assert.Equal(EventTarget.All, envelope.Target);
        Assert.Empty(envelope.TargetIds);
    }

    [Fact]
    public async Task SendAsync_SameEventTwice_GivesEachEnvelopeItsOwnId()
    {
        var sender = CreateSender();
        var created = IntegrationEventFactory.PlayerCreated();

        await sender.SendAsync(created, EventAudience.Everyone);
        await sender.SendAsync(created, EventAudience.Everyone);

        Assert.Equal(2, _published.Count);
        Assert.NotEqual(_published[0].Id, _published[1].Id);
    }

    [Fact]
    public async Task SendAsync_EmptyPlayerAudience_ThrowsAndPublishesNothing()
    {
        var sender = CreateSender();

        await Assert.ThrowsAsync<ArgumentException>(
            () => sender.SendAsync(IntegrationEventFactory.PlayerCreated(), EventAudience.Players()));

        _daprClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_EmptyGameAudience_ThrowsAndPublishesNothing()
    {
        var sender = CreateSender();

        await Assert.ThrowsAsync<ArgumentException>(
            () => sender.SendAsync(IntegrationEventFactory.GameStarted(), EventAudience.Game([])));

        _daprClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_DefaultAudience_ThrowsAndPublishesNothing()
    {
        var sender = CreateSender();

        await Assert.ThrowsAsync<ArgumentException>(
            () => sender.SendAsync(IntegrationEventFactory.GameStarted(), default));

        _daprClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_PassesTheCancellationTokenToDapr()
    {
        using var cancellation = new CancellationTokenSource();

        await CreateSender().SendAsync(IntegrationEventFactory.GameCreated(), EventAudience.Everyone, cancellation.Token);

        _daprClient.Verify(c => c.PublishEventAsync(
            "pubsub", "integration-events", It.IsAny<IntegrationEventEnvelope>(), cancellation.Token));
    }

    [Fact]
    public async Task SendAsync_PublishesInAProducerSpanWithTheMessagingAttributes()
    {
        using var activities = new ActivityCollector();

        await CreateSender().SendAsync(IntegrationEventFactory.GameStarted(), EventAudience.Everyone);

        var span = activities.Single("publish integration-events");
        var envelope = Assert.Single(_published);
        Assert.Equal(ActivityKind.Producer, span.Kind);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal("dapr", span.GetTagItem(IntegrationEventsTelemetry.MessagingSystemAttribute));
        Assert.Equal("integration-events", span.GetTagItem(IntegrationEventsTelemetry.DestinationNameAttribute));
        Assert.Equal("publish", span.GetTagItem(IntegrationEventsTelemetry.OperationTypeAttribute));
        Assert.Equal(envelope.Id, span.GetTagItem(IntegrationEventsTelemetry.MessageIdAttribute));
        Assert.Equal("game.started.v1", span.GetTagItem(IntegrationEventsTelemetry.EventTypeAttribute));
    }

    [Fact]
    public async Task SendAsync_CountsThePublishedEventByType()
    {
        using var published = Collect(IntegrationEventsMetrics.PublishedName);
        using var failed = Collect(IntegrationEventsMetrics.PublishFailedName);

        await CreateSender().SendAsync(IntegrationEventFactory.GameStarted(), EventAudience.Everyone);

        var measurement = Assert.Single(published.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal("game.started.v1", measurement.Tags[IntegrationEventsTelemetry.EventTypeAttribute]);
        Assert.Empty(failed.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task SendAsync_SidecarFailure_EndsTheSpanInErrorCountsItAndRethrows()
    {
        using var activities = new ActivityCollector();
        using var published = Collect(IntegrationEventsMetrics.PublishedName);
        using var failed = Collect(IntegrationEventsMetrics.PublishFailedName);
        var failure = new InvalidOperationException("The sidecar is down.");
        _daprClient
            .Setup(c => c.PublishEventAsync(
                "pubsub", "integration-events", It.IsAny<IntegrationEventEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSender().SendAsync(IntegrationEventFactory.GameStarted(), EventAudience.Everyone));

        Assert.Same(failure, thrown);
        var span = activities.Single("publish integration-events");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(failure.Message, span.StatusDescription);
        Assert.Contains(span.Events, e => e.Name == "exception");
        var measurement = Assert.Single(failed.GetMeasurementSnapshot());
        Assert.Equal("game.started.v1", measurement.Tags[IntegrationEventsTelemetry.EventTypeAttribute]);
        Assert.Empty(published.GetMeasurementSnapshot());
    }

    private MetricCollector<long> Collect(string instrumentName) =>
        new(_meterFactory, IntegrationEventsTelemetry.Name, instrumentName);
}
