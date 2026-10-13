namespace HexMaster.Battleship.Games.DomainModels;

// What marking a player ready did: nothing because the player already was, made the player ready,
// or made the player ready and, both players now being ready, started the game.
public enum ReadyOutcome
{
    AlreadyReady,
    Ready,
    ReadyAndStarted,
}
