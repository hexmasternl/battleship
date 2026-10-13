namespace HexMaster.Battleship.IntegrationEvents.Games;

public sealed record GameStartedEvent(Guid GameId, string GameCode, Guid PlayerWithTurnId) : IIntegrationEvent
{
    public static string EventType => "game.started.v1";
}
