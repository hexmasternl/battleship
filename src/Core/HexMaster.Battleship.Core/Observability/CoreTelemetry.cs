using System.Diagnostics;

namespace HexMaster.Battleship.Core.Observability;

// The tracing source of the shared kernel and the names of the attributes and tags it writes.
// Every module names its source and meter after its assembly, so the service defaults register
// them all with one HexMaster.Battleship.* wildcard (ADR 0008).
public static class CoreTelemetry
{
    public const string Name = "HexMaster.Battleship.Core";

    public static readonly ActivitySource ActivitySource = new(Name);

    // Logger category of the use-case decorators.
    public const string UseCaseLoggerCategory = "HexMaster.Battleship.Core.UseCases";

    // Span attributes.
    public const string UseCaseAttribute = "battleship.use_case";
    public const string UseCaseKindAttribute = "battleship.use_case.kind";

    // Metric tags; both come from closed sets.
    public const string UseCaseTag = "use_case";
    public const string OutcomeTag = "outcome";

    public const string CommandKind = "command";
    public const string QueryKind = "query";
    public const string SuccessOutcome = "success";
    public const string FailureOutcome = "failure";
}
