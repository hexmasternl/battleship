namespace HexMaster.Battleship.Core.Cqrs;

// Handles a command that produces a result. Handlers stay free of HTTP concerns (ADR 0004).
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}

// Handles a command without a result; the endpoint answers 202 Accepted on success.
public interface ICommandHandler<in TCommand>
{
    Task Handle(TCommand command, CancellationToken cancellationToken);
}
