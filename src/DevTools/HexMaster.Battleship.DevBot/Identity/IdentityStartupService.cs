namespace HexMaster.Battleship.DevBot.Identity;

// Creates the bot's user when the worker starts, so it exists before the first game arrives.
// Retries until the Users service answers; the gateway may still be warming up.
public sealed partial class IdentityStartupService(TokenStore tokenStore, TimeProvider timeProvider, ILogger<IdentityStartupService> logger)
    : BackgroundService
{
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await tokenStore.GetUserIdAsync(stoppingToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogUserNotCreated(logger, exception, RetryDelay);
                await Task.Delay(RetryDelay, timeProvider, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the dev bot user; retrying in {Delay}.")]
    private static partial void LogUserNotCreated(ILogger logger, Exception exception, TimeSpan delay);
}
