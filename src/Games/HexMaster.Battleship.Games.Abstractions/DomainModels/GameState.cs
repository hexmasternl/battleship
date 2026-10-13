namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// Where a game is in its life. A game starts New, waiting for a second player.
public enum GameState
{
    New,
    Started,
    Completed,
    Abandoned,
}
