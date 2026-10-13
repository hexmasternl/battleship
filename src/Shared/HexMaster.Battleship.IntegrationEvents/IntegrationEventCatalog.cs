using HexMaster.Battleship.IntegrationEvents.Games;
using HexMaster.Battleship.IntegrationEvents.Players;

namespace HexMaster.Battleship.IntegrationEvents;

// Every integration event the backend publishes, with its wire name. The one place to look up
// what exists; a new event, or a new version of one, is added here.
public static class IntegrationEventCatalog
{
    public static IReadOnlyList<IntegrationEventDescriptor> Events { get; } =
    [
        Describe<PlayerCreatedEvent>(),
        Describe<PlayerUpdatedEvent>(),
        Describe<GameCreatedEvent>(),
        Describe<GameJoinedEvent>(),
        Describe<GamePlayerReadyEvent>(),
        Describe<GameStartedEvent>(),
        Describe<GameShotFiredEvent>(),
        Describe<GameTurnChangedEvent>(),
        Describe<GameEndedEvent>()
    ];

    private static IntegrationEventDescriptor Describe<TEvent>() where TEvent : IIntegrationEvent =>
        new(typeof(TEvent), TEvent.EventType);
}

public sealed record IntegrationEventDescriptor(Type ClrType, string EventType);
