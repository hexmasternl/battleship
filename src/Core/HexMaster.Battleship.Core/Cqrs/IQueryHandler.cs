namespace HexMaster.Battleship.Core.Cqrs;

// Handles a query and returns a DTO record; a query never changes state (ADR 0004).
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
