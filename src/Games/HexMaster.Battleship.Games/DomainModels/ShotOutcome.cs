using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.DomainModels;

// What a recorded shot did: the cell it was fired at, whether it hit one of the opponent's ships and
// whether that hit sank the opponent's whole fleet and so completed the game.
public sealed record ShotOutcome(Coordinate At, bool Hit, bool GameCompleted = false);
