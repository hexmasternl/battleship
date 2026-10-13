namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// The game the caller joined, or already played in: its id and code.
public sealed record JoinGameResponse(Guid GameId, string GameCode);
