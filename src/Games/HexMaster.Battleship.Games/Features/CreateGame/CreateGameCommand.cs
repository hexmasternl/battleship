namespace HexMaster.Battleship.Games.Features.CreateGame;

// Creates a new game with the calling player as player one. The identity comes from the access token.
public sealed record CreateGameCommand(Guid PlayerId, string PlayerName);
