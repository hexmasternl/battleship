using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Aspire.AppHost.DevBot;

/// <summary>
/// Shows the bot's state on its resource: polls <c>GET /admin/state</c> every two seconds once the
/// bot is ready, and publishes the preset, auto-join and the games in play as resource properties
/// when they change. A bot restart starts a fresh loop; the previous one is cancelled.
/// </summary>
internal sealed class BotStateMirror(
    Func<CancellationToken, Task<BotState?>> readState,
    Func<Func<CustomResourceSnapshot, CustomResourceSnapshot>, Task> publish,
    TimeProvider timeProvider,
    ILogger logger)
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public const string NoGames = "none";

    private BotState? published;

    public static IResourceBuilder<BattleshipBotResource> Attach(IResourceBuilder<BattleshipBotResource> builder)
    {
        CancellationTokenSource? current = null;

        return builder.OnResourceReady((bot, readyEvent, cancellationToken) =>
        {
            var services = readyEvent.Services;
            var notifications = services.GetRequiredService<ResourceNotificationService>();
            var stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(bot);

            var next = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            Interlocked.Exchange(ref current, next)?.Cancel();

            var mirror = new BotStateMirror(
                cancellationToken => BotAdminClient.For(bot).GetStateAsync(cancellationToken),
                update => notifications.PublishUpdateAsync(bot, update),
                TimeProvider.System,
                logger);

            _ = mirror.RunAsync(next.Token);
            return Task.CompletedTask;
        });
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider);
        try
        {
            do
            {
                await PollOnceAsync(cancellationToken);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    // Returns whether new properties were published. A bot that does not answer leaves them as they are.
    public async Task<bool> PollOnceAsync(CancellationToken cancellationToken)
    {
        BotState? state;
        try
        {
            state = await readState(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(exception, "The dev bot did not answer the state poll.");
            return false;
        }

        if (state is null || SameState(state, published))
        {
            return false;
        }

        await publish(snapshot => Apply(snapshot, state));
        published = state;
        return true;
    }

    public static CustomResourceSnapshot Apply(CustomResourceSnapshot snapshot, BotState state) => snapshot with
    {
        Properties =
        [
            .. snapshot.Properties.Where(p => p.Name is not (BattleshipBotResource.IntelligenceProperty
                or BattleshipBotResource.AutoJoinProperty or BattleshipBotResource.GamesProperty)),
            new(BattleshipBotResource.IntelligenceProperty, state.Intelligence),
            new(BattleshipBotResource.AutoJoinProperty, state.AutoJoin),
            new(BattleshipBotResource.GamesProperty, state.Games.Count == 0 ? NoGames : string.Join(", ", state.Games)),
        ],
    };

    private static bool SameState(BotState state, BotState? other) =>
        other is not null
        && state.Intelligence == other.Intelligence
        && state.AutoJoin == other.AutoJoin
        && state.Games.SequenceEqual(other.Games);
}
