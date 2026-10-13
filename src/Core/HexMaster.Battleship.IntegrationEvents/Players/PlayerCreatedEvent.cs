namespace HexMaster.Battleship.IntegrationEvents.Players;

public sealed record PlayerCreatedEvent(Guid PlayerId, string PlayerName) : IIntegrationEvent
{
    public static string EventType => "player.created.v1";
}
