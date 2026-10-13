using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Connections;

namespace HexMaster.Battleship.Events.Routing;

public interface IEventRouter
{
    // Opens a connection for the user. With a last event id, the buffered events the user
    // missed are queued first, ahead of any event routed afterwards.
    EventConnection Connect(string userId, long? lastEventId);

    void Disconnect(EventConnection connection);

    // Assigns the next sequence number and writes the event to every connection it targets.
    RouteResult Route(IntegrationEventEnvelope envelope);
}

public sealed record RouteResult(long Sequence, int DeliveredTo);
