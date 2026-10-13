namespace HexMaster.Battleship.IntegrationEvents.Players;

// The player's name changed.
public sealed record PlayerUpdatedEvent(Guid PlayerId, string PlayerName) : IIntegrationEvent
{
    public static string EventType => "player.updated.v1";
}
