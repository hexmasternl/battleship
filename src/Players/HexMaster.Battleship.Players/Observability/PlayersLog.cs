using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Players.Observability;

// Log messages of the Players module; event ids 1000-1999. Never log a token or a display name.
internal static partial class PlayersLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Issued a token pair to new user {UserId}")]
    public static partial void TokensIssued(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Refreshed the token pair of user {UserId}")]
    public static partial void TokensRefreshed(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Refused a refresh token: {Reason}")]
    public static partial void TokenRefused(ILogger logger, string reason);
}
