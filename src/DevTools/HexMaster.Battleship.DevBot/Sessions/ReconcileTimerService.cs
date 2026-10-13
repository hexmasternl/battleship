namespace HexMaster.Battleship.DevBot.Sessions;

// The safety net under the events: every interval, each game the bot plays is reconciled, so a lost
// turn event delays the bot's shot by at most one interval instead of stalling the game.
public sealed partial class ReconcileTimerService(
    GameSessions sessions,
    BotSettings settings,
    TimeProvider timeProvider,
    ILogger<ReconcileTimerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(settings.ReconcileInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await sessions.ReconcileAllAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogReconcileFailed(logger, exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconciling the dev bot's games failed; trying again on the next tick.")]
    private static partial void LogReconcileFailed(ILogger logger, Exception exception);
}
