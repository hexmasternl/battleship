namespace HexMaster.Battleship.IntegrationEvents.Games;

// PlayerId is the player who created the game.
public sealed record GameCreatedEvent(Guid GameId, string GameCode, Guid PlayerId) : IIntegrationEvent
{
    public static string EventType => "game.created.v1";
}
