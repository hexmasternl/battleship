using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Abstractions.Repositories;

// A stored game read for an update, with the opaque token that identifies the stored version.
// Passing the token back to TryUpdateAsync stores the update only if nobody changed the game since.
public sealed record GameForUpdate(IGame Game, string ConcurrencyToken);
