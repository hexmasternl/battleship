using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Client;

// The games API as the bot uses it, through the gateway, as the bot's own user. Every call answers
// a result instead of throwing for an API refusal; only transport failures throw.
public interface IGamesClient
{
    Task<GameCallResult<JoinGameResponse>> JoinAsync(string code, CancellationToken cancellationToken);

    Task<GameCallResult<GameView>> GetGameAsync(string code, CancellationToken cancellationToken);

    Task<GameCallResult<GameView>> PlaceShipAsync(string code, ShipPlacement placement, CancellationToken cancellationToken);

    Task<GameCallResult<GameView>> ReadyAsync(string code, CancellationToken cancellationToken);

    Task<GameCallResult<FireShotResponse>> FireAsync(string code, Coordinate at, CancellationToken cancellationToken);
}
