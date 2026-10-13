namespace HexMaster.Battleship.IntegrationEvents.Games;

// The turn passed to PlayerWithTurnId after a shot. Follows game.shot-fired.v1 for the same shot.
public sealed record GameTurnChangedEvent(Guid GameId, string GameCode, Guid PlayerWithTurnId) : IIntegrationEvent
{
    public static string EventType => "game.turn-changed.v1";
}
