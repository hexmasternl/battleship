namespace HexMaster.Battleship.IntegrationEvents;

// Publishes a catalog event, wrapped in the integration event envelope, to the shared
// integration-events topic. Only catalog events compile as TEvent.
public interface IIntegrationEventSender
{
    // Throws ArgumentException, before anything is published, when the envelope would be
    // invalid, for example a players or game audience without player ids.
    Task SendAsync<TEvent>(TEvent integrationEvent, EventAudience audience, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
