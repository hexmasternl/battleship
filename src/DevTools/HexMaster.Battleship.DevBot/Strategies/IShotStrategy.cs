using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Strategies;

// Chooses the bot's next shot from the shots it fired so far. Pure: the same shots and the same
// Random state give the same cell, and the cell is never one already shot at.
public interface IShotStrategy
{
    // Throws InvalidOperationException when every cell of the board has been shot at.
    Coordinate NextShot(IReadOnlyList<ShotView> myShots, Random random);
}
