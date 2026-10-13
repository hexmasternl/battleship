using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Features.PlaceShip;

// Places or moves one ship of the calling player. The identity comes from the access token.
public sealed record PlaceShipCommand(GameCode Code, Guid PlayerId, ShipPlacement Placement);
