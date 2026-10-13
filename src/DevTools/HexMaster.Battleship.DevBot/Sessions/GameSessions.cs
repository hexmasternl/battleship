using System.Collections.Concurrent;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Observability;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.DevBot.Sessions;

// Every game the bot plays, by code. A session leaves the registry when its game ends.
public sealed partial class GameSessions(
    IGamesClient gamesClient,
    BotSettings settings,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory,
    IHostApplicationLifetime lifetime)
{
    private readonly ConcurrentDictionary<string, GameSession> sessions = new(StringComparer.Ordinal);
    private readonly ILogger logger = loggerFactory.CreateLogger<GameSessions>();

    // Each session gets its own Random, seeded from the code and the time it started. Tests replace it.
    internal Func<string, Random> RandomFor { get; init; } =
        code => new Random(HashCode.Combine(code, timeProvider.GetUtcNow().UtcTicks));

    public IReadOnlyList<string> ActiveCodes => [.. sessions.Keys.Order(StringComparer.Ordinal)];

    public bool IsActive(string code) => sessions.ContainsKey(Normalize(code));

    // Takes the seat, or confirms the bot already plays in the game, and starts tracking it.
    public async Task<GameCallResult<JoinGameResponse>> JoinAsync(string code, CancellationToken cancellationToken)
    {
        code = Normalize(code);
        using var activity = DevBotTelemetry.ActivitySource.StartActivity("devbot.join");
        activity?.SetTag(DevBotTelemetry.GameCodeAttribute, code);

        var result = await gamesClient.JoinAsync(code, cancellationToken);
        activity?.SetTag(DevBotTelemetry.OutcomeAttribute, result.Status.ToString());
        if (result.IsOk)
        {
            Track(code);
            LogJoined(logger, code);
        }

        return result;
    }

    public GameSession Track(string code)
    {
        code = Normalize(code);
        return sessions.GetOrAdd(code, c => new GameSession(
            c, gamesClient, settings, timeProvider, RandomFor(c), loggerFactory.CreateLogger<GameSession>()));
    }

    // Reconciles a tracked game; an untracked code is ignored.
    public async Task ReconcileAsync(string code, CancellationToken cancellationToken)
    {
        code = Normalize(code);
        if (!sessions.TryGetValue(code, out var session))
        {
            return;
        }

        await session.ReconcileAsync(cancellationToken);
        if (session.IsEnded)
        {
            sessions.TryRemove(new KeyValuePair<string, GameSession>(code, session));
        }
    }

    public async Task ReconcileAllAsync(CancellationToken cancellationToken)
    {
        foreach (var code in ActiveCodes)
        {
            await ReconcileAsync(code, cancellationToken);
        }
    }

    // Reconciles in the background, so a Dapr delivery or an admin call is answered at once.
    public void TriggerReconcile(string code) => RunInBackground(code, ReconcileAsync);

    // Waits the grace period, then joins and plays unless auto-join was switched off meanwhile or
    // someone else took the seat.
    public void ScheduleJoin(string code) => RunInBackground(code, async (c, cancellationToken) =>
    {
        await Task.Delay(settings.JoinGracePeriod, timeProvider, cancellationToken);
        if (!settings.AutoJoin || IsActive(c))
        {
            return;
        }

        var result = await JoinAsync(c, cancellationToken);
        if (result.IsOk)
        {
            await ReconcileAsync(c, cancellationToken);
        }
        else
        {
            LogJoinSkipped(logger, c, result.Status);
        }
    });

    internal Task? LastBackgroundTask { get; private set; }

    private void RunInBackground(string code, Func<string, CancellationToken, Task> work)
    {
        code = Normalize(code);
        var stopping = lifetime.ApplicationStopping;
        LastBackgroundTask = Task.Run(async () =>
        {
            try
            {
                await work(code, stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                LogBackgroundFailed(logger, exception, code);
            }
        }, CancellationToken.None);
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    [LoggerMessage(Level = LogLevel.Information, Message = "The dev bot joined game {GameCode}.")]
    private static partial void LogJoined(ILogger logger, string gameCode);

    // A human took the seat, the game started or it is gone: expected, so only a debug line.
    [LoggerMessage(Level = LogLevel.Debug, Message = "The dev bot did not join game {GameCode}: {Status}.")]
    private static partial void LogJoinSkipped(ILogger logger, string gameCode, GameCallStatus status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Playing game {GameCode} failed; the next event or the reconcile timer tries again.")]
    private static partial void LogBackgroundFailed(ILogger logger, Exception exception, string gameCode);
}
