using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.DomainModels;

// What a recorded shot did: the cell it was fired at and whether it hit one of the opponent's ships.
public sealed record ShotOutcome(Coordinate At, bool Hit);
