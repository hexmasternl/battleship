namespace HexMaster.Battleship.IntegrationEvents.Games;

// PlayerId fired at the opponent's board cell (Column, Row).
public sealed record GameShotFiredEvent(Guid GameId, string GameCode, Guid PlayerId, int Column, int Row) : IIntegrationEvent
{
    public static string EventType => "game.shot-fired.v1";
}
