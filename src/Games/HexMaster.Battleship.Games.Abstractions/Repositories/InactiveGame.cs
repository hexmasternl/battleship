namespace HexMaster.Battleship.Games.Abstractions.Repositories;

// A stored game selected for removal: its code and the token of the stored version it was selected
// at. Passing the token to TryDeleteAsync deletes the game only if nobody changed it since.
public sealed record InactiveGame(string GameCode, string ConcurrencyToken);
