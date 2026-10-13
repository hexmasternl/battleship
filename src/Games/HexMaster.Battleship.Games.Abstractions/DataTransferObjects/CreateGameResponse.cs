namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// The game that was created: its id, and the code players use to find it.
public sealed record CreateGameResponse(Guid GameId, string GameCode);
