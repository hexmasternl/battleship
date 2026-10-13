using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Games.Observability;

// Log messages of the Games module; event ids 4000-4999. Never log a player name.
internal static partial class GamesLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Created game {GameId} with code {GameCode} for player {PlayerId}")]
    public static partial void GameCreated(ILogger logger, Guid gameId, string gameCode, Guid playerId);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Game code {GameCode} is already in use (attempt {Attempt} of {MaxAttempts})")]
    public static partial void GameCodeInUse(ILogger logger, string gameCode, int attempt, int maxAttempts);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Publishing {EventType} for game {GameCode} failed; the game was created")]
    public static partial void GameCreatedAnnouncementFailed(ILogger logger, Exception exception, string eventType, string gameCode);
}
