using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Core.Observability;

// Runs a handler inside its use-case span and records its duration and outcome.
internal static class UseCaseInstrumentation
{
    // CreateTokensCommand becomes CreateTokens, GetCurrentUserQuery becomes GetCurrentUser.
    public static string NameOf(Type requestType, string suffix)
    {
        var name = requestType.Name;
        var arity = name.IndexOf('`', StringComparison.Ordinal);
        if (arity >= 0)
        {
            name = name[..arity];
        }

        return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }

    public static async Task<TResult> RunAsync<TResult>(
        string useCase,
        string kind,
        CoreMetrics metrics,
        ILogger logger,
        Func<Task<TResult>> handle)
    {
        using var activity = CoreTelemetry.ActivitySource.StartActivity(useCase, ActivityKind.Internal);
        activity?.SetTag(CoreTelemetry.UseCaseAttribute, useCase);
        activity?.SetTag(CoreTelemetry.UseCaseKindAttribute, kind);

        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await handle();

            activity?.SetStatus(ActivityStatusCode.Ok);
            metrics.UseCaseCompleted(useCase, succeeded: true, Stopwatch.GetElapsedTime(started));

            return result;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            metrics.UseCaseCompleted(useCase, succeeded: false, Stopwatch.GetElapsedTime(started));
            CoreLog.UseCaseFailed(logger, useCase, exception.GetType().Name);

            throw;
        }
    }
}
