using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.Events.Routing;

public interface IEventRouter
{
    // Assigns the next sequence number and writes the event to every connection it targets.
    RouteResult Route(IntegrationEventEnvelope envelope);
}

public sealed record RouteResult(long Sequence, int DeliveredTo);
