using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Observability;

namespace HexMaster.Battleship.Games.Features.GetGame;

// Returns null when no game has the code or the caller does not play in it; the endpoint answers
// both the same way, so a code cannot be probed for games of other players.
public sealed class GetGameQueryHandler(IGameRepository games) : IQueryHandler<GetGameQuery, GameView?>
{
    public async Task<GameView?> Handle(GetGameQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The use-case span comes from the handler decorator; this handler only adds attributes.
        var activity = Activity.Current;
        activity?.SetTag(GamesTelemetry.GameCodeAttribute, query.Code.Value);
        activity?.SetTag(GamesTelemetry.PlayerIdAttribute, query.PlayerId.ToString());

        var game = await games.GetByCodeAsync(query.Code.Value, cancellationToken);
        if (game is null || !(game.PlayerOne.PlayerId == query.PlayerId || game.PlayerTwo?.PlayerId == query.PlayerId))
        {
            return null;
        }

        activity?.SetTag(GamesTelemetry.GameIdAttribute, game.Id.ToString());
        return GameViewMapper.ToView(game, query.PlayerId);
    }
}
