using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Features.JoinGame;

// Seats the calling player as player two of the game with this code. The identity comes from the
// access token.
public sealed record JoinGameCommand(GameCode Code, Guid PlayerId, string PlayerName);

// The joined game, or Game null when no game has the code. Refusals surface as
// GameJoinRefusedException and conflicts as GameUpdateConflictException.
public sealed record JoinGameResult(JoinGameResponse? Game)
{
    public static JoinGameResult NotFound { get; } = new((JoinGameResponse?)null);

    public static JoinGameResult Success(JoinGameResponse game) => new(game);
}
