namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// One of the two players of a game. Only the game changes a player, so the turn rule holds.
public interface IPlayer
{
    Guid PlayerId { get; }

    string PlayerName { get; }

    bool HasTurn { get; }

    IReadOnlyList<Coordinate> ShipCoordinates { get; }

    IReadOnlyList<Coordinate> ShotCoordinates { get; }

    DateTimeOffset LastActivityOn { get; }
}
