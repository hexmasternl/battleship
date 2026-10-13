namespace HexMaster.Battleship.Games.Services;

// Chooses which player shoots first when a game starts. Injected so tests can decide.
public interface IFirstTurnPicker
{
    Guid Pick(Guid playerOneId, Guid playerTwoId);
}
