using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// A game of Battleship between the player who created it and, once joined, a second player.
// State is the storage lifecycle of the model; GameState is where the game itself stands.
public interface IGame : IDomainModel<Guid>
{
    // Eight characters, A-Z and 0-9, unique among all games. Players share it to find the game.
    string GameCode { get; }

    GameState GameState { get; }

    IPlayer PlayerOne { get; }

    // Empty until a second player joins.
    IPlayer? PlayerTwo { get; }

    DateTimeOffset LastActivityOn { get; }

    // The player who sank the opponent's fleet; empty until the game is completed.
    Guid? WinnerId { get; }

    // Gives the turn to one of the players of a started game in which nobody has the turn yet.
    void GiveFirstTurnTo(Guid playerId, DateTimeOffset now);

    // Moves the turn from the player who has it to the other player.
    void SwitchTurn(DateTimeOffset now);
}
