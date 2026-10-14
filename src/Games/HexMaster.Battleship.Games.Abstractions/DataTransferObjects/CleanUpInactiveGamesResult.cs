namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// What a cleanup run did: games removed, and games selected but kept because they were already
// gone or had changed since they were selected.
public sealed record CleanUpInactiveGamesResult(int Removed, int Skipped);
