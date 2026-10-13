using System.Text.Json;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Sessions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.IntegrationEvents.Games;

namespace HexMaster.Battleship.DevBot.Events;

// What the bot did with one delivered envelope. Every delivery is acknowledged whatever this is.
public enum EventDisposition
{
    Ignored,
    Stale,
    OwnGame,
    AutoJoinOff,
    JoinScheduled,
    ReconcileTriggered,
}

// Turns integration events into triggers. A created game from another player is a join candidate;
// an event of a game the bot plays triggers a reconcile of that game. Nothing in a payload is
// trusted beyond the game code: the session reads the game from the API.
public sealed partial class IntegrationEventHandler(
    GameSessions sessions,
    BotSettings settings,
    TokenStore tokenStore,
    TimeProvider timeProvider,
    ILogger<IntegrationEventHandler> logger)
{
    // The bot's queue is durable, so a restarted bot is handed old envelopes. Anything older than
    // this beyond the grace period is history, not a game to join.
    internal static readonly TimeSpan FreshnessMargin = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> GameEventTypes =
    [
        GameJoinedEvent.EventType,
        GamePlayerReadyEvent.EventType,
        GameStartedEvent.EventType,
        GameTurnChangedEvent.EventType,
        GameEndedEvent.EventType,
    ];

    public async Task<EventDisposition> HandleAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var isCreated = envelope.Type == GameCreatedEvent.EventType;
        if (!isCreated && !GameEventTypes.Contains(envelope.Type))
        {
            return EventDisposition.Ignored;
        }

        if (timeProvider.GetUtcNow() - envelope.OccurredAt > settings.JoinGracePeriod + FreshnessMargin)
        {
            LogStale(logger, envelope.Id, envelope.Type, envelope.OccurredAt);
            return EventDisposition.Stale;
        }

        var gameCode = ReadGameCode(envelope.Payload);
        if (gameCode is null)
        {
            return EventDisposition.Ignored;
        }

        var botId = await tokenStore.GetUserIdAsync(cancellationToken);

        if (isCreated)
        {
            var created = envelope.Payload.Deserialize<GameCreatedEvent>(PayloadOptions);
            if (created?.PlayerId == botId)
            {
                return EventDisposition.OwnGame;
            }

            if (!settings.AutoJoin)
            {
                return EventDisposition.AutoJoinOff;
            }

            sessions.ScheduleJoin(gameCode);
            LogJoinScheduled(logger, gameCode, settings.JoinGracePeriod);
            return EventDisposition.JoinScheduled;
        }

        var forBot = envelope.TargetIds?.Any(id => Guid.TryParse(id, out var target) && target == botId) == true;
        if (!forBot || !sessions.IsActive(gameCode))
        {
            return EventDisposition.Ignored;
        }

        sessions.TriggerReconcile(gameCode);
        return EventDisposition.ReconcileTriggered;
    }

    private static string? ReadGameCode(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty("gameCode", out var code)
        && code.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(code.GetString())
            ? code.GetString()
            : null;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignored stale envelope {EnvelopeId} ({EventType}) from {OccurredAt}.")]
    private static partial void LogStale(ILogger logger, string envelopeId, string eventType, DateTimeOffset occurredAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Game {GameCode} was created; the dev bot joins after {GracePeriod} unless someone else does.")]
    private static partial void LogJoinScheduled(ILogger logger, string gameCode, TimeSpan gracePeriod);
}
