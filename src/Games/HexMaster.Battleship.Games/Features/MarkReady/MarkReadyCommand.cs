using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Features.MarkReady;

// Marks the calling player ready once their fleet is placed. The identity comes from the access token.
public sealed record MarkReadyCommand(GameCode Code, Guid PlayerId);
