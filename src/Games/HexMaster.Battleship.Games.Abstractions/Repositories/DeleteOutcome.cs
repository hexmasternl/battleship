namespace HexMaster.Battleship.Games.Abstractions.Repositories;

// What a conditional delete did: removed the game, found it already gone, or found it changed since
// it was selected and kept it.
public enum DeleteOutcome
{
    Deleted,
    NotFound,
    Changed,
}
