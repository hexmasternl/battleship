using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.Core.Observability;

// The metrics every use case emits, whichever module it belongs to.
public sealed class CoreMetrics
{
    public const string UseCaseDurationName = "battleship.use_case.duration";

    private readonly Histogram<double> _useCaseDuration;

    public CoreMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(CoreTelemetry.Name);
        _useCaseDuration = meter.CreateHistogram<double>(
            UseCaseDurationName,
            unit: "s",
            description: "Duration of a command or query handler, by use case and outcome.");
    }

    public void UseCaseCompleted(string useCase, bool succeeded, TimeSpan elapsed) =>
        _useCaseDuration.Record(
            elapsed.TotalSeconds,
            new KeyValuePair<string, object?>(CoreTelemetry.UseCaseTag, useCase),
            new KeyValuePair<string, object?>(
                CoreTelemetry.OutcomeTag,
                succeeded ? CoreTelemetry.SuccessOutcome : CoreTelemetry.FailureOutcome));
}
