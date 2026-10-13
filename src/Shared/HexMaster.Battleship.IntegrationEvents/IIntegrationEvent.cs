namespace HexMaster.Battleship.IntegrationEvents;

// A member of the integration event catalog. EventType is the versioned wire name
// (<subject>.<action>.v<version>) that becomes the envelope type and the event name clients
// receive. A breaking payload change adds a new record with the next version next to this one.
public interface IIntegrationEvent
{
    static abstract string EventType { get; }
}
