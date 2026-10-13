namespace HexMaster.Battleship.IntegrationEvents.Games;

// A player placed the whole fleet and is ready. When both players are, game.started.v1 follows.
public sealed record GamePlayerReadyEvent(Guid GameId, string GameCode, Guid PlayerId) : IIntegrationEvent
{
    public static string EventType => "game.player-ready.v1";
}
