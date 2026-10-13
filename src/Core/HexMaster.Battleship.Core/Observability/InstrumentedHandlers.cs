using HexMaster.Battleship.Core.Cqrs;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Core.Observability;

// Decorators that give every command and query handler its use-case span and duration metric,
// so a handler never opens its own span (ADR 0008). Registered by AddCommandHandler and
// AddQueryHandler; a handler adds domain attributes to Activity.Current, which is this span.
public sealed class InstrumentedCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    CoreMetrics metrics,
    ILogger logger) : ICommandHandler<TCommand, TResult>
{
    public static readonly string UseCase = UseCaseInstrumentation.NameOf(typeof(TCommand), "Command");

    public ICommandHandler<TCommand, TResult> Inner => inner;

    public Task<TResult> Handle(TCommand command, CancellationToken cancellationToken) =>
        UseCaseInstrumentation.RunAsync(
            UseCase, CoreTelemetry.CommandKind, metrics, logger, () => inner.Handle(command, cancellationToken));
}

public sealed class InstrumentedCommandHandler<TCommand>(
    ICommandHandler<TCommand> inner,
    CoreMetrics metrics,
    ILogger logger) : ICommandHandler<TCommand>
{
    public static readonly string UseCase = UseCaseInstrumentation.NameOf(typeof(TCommand), "Command");

    public ICommandHandler<TCommand> Inner => inner;

    public Task Handle(TCommand command, CancellationToken cancellationToken) =>
        UseCaseInstrumentation.RunAsync(
            UseCase, CoreTelemetry.CommandKind, metrics, logger, async () =>
            {
                await inner.Handle(command, cancellationToken);
                return true;
            });
}

public sealed class InstrumentedQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    CoreMetrics metrics,
    ILogger logger) : IQueryHandler<TQuery, TResult>
{
    public static readonly string UseCase = UseCaseInstrumentation.NameOf(typeof(TQuery), "Query");

    public IQueryHandler<TQuery, TResult> Inner => inner;

    public Task<TResult> Handle(TQuery query, CancellationToken cancellationToken) =>
        UseCaseInstrumentation.RunAsync(
            UseCase, CoreTelemetry.QueryKind, metrics, logger, () => inner.Handle(query, cancellationToken));
}
