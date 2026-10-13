namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// The body of POST /games/{code}/shots: the cell fired at, zero-based. Both fields are optional here
// so the endpoint can answer a missing one with a validation problem.
public sealed record FireShotRequest(int? Column, int? Row);
