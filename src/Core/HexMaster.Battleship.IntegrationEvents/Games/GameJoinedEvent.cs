namespace HexMaster.Battleship.IntegrationEvents.Games;

// The second player joined the game.
public sealed record GameJoinedEvent(Guid GameId, string GameCode, Guid PlayerId, string PlayerName) : IIntegrationEvent
{
    public static string EventType => "game.joined.v1";
}
