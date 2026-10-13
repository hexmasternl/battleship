namespace HexMaster.Battleship.Players.Features.CreateTokens;

// Creates an anonymous user and issues its first token pair. Without a display name the user
// gets a generated call sign.
public sealed record CreateTokensCommand(string? DisplayName);
