namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// One of the two players of a game. Only the game changes a player, so the turn rule holds.
public interface IPlayer
{
    Guid PlayerId { get; }

    string PlayerName { get; }

    bool HasTurn { get; }

    // Set once the whole fleet is placed and the player said so; never unset.
    bool IsReady { get; }

    // At most one placement per ship kind.
    IReadOnlyList<ShipPlacement> Ships { get; }

    IReadOnlyList<Shot> Shots { get; }

    DateTimeOffset LastActivityOn { get; }
}
