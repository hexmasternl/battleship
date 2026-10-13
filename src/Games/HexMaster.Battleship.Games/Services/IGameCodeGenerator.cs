using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Services;

public interface IGameCodeGenerator
{
    // A random code. Uniqueness is checked when the game is stored.
    GameCode Generate();
}
