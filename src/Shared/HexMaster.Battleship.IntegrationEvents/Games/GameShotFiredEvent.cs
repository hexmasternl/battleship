namespace HexMaster.Battleship.IntegrationEvents.Games;

// PlayerId fired at the opponent's board cell (Column, Row); Hit tells whether a ship covers it.
public sealed record GameShotFiredEvent(Guid GameId, string GameCode, Guid PlayerId, int Column, int Row, bool Hit) : IIntegrationEvent
{
    public static string EventType => "game.shot-fired.v1";
}
