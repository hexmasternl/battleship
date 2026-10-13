using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Features.GetGame;

// Reads the game with this code as the calling player sees it. The identity comes from the access token.
public sealed record GetGameQuery(GameCode Code, Guid PlayerId);
