using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Features.FireShot;

// Fires the calling player's shot at a cell of the opponent's board. The identity comes from the
// access token; the cell is on the board, the endpoint checked it.
public sealed record FireShotCommand(GameCode Code, Guid PlayerId, Coordinate At);
