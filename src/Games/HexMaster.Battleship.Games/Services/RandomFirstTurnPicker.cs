namespace HexMaster.Battleship.Games.Services;

// A fair coin. Who shoots first needs no cryptographic randomness.
public sealed class RandomFirstTurnPicker : IFirstTurnPicker
{
    public Guid Pick(Guid playerOneId, Guid playerTwoId) =>
        Random.Shared.Next(2) == 0 ? playerOneId : playerTwoId;
}
