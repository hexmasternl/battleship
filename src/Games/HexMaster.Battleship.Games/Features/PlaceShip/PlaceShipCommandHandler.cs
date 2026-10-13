using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Features.GetGame;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Services;

namespace HexMaster.Battleship.Games.Features.PlaceShip;

// Returns the caller's view after the placement, or null when no game has the code or the caller
// does not play in it. Refusals surface as PlacementRefusedException, conflicts as
// GameUpdateConflictException; neither stores anything.
public sealed class PlaceShipCommandHandler(IGameUpdater updater, TimeProvider time)
    : ICommandHandler<PlaceShipCommand, GameView?>
{
    public async Task<GameView?> Handle(PlaceShipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The use-case span comes from the handler decorator; this handler only adds attributes.
        var activity = Activity.Current;
        activity?.SetTag(GamesTelemetry.GameCodeAttribute, command.Code.Value);
        activity?.SetTag(GamesTelemetry.PlayerIdAttribute, command.PlayerId.ToString());
        activity?.SetTag(GamesTelemetry.ShipKindAttribute, command.Placement.Kind.ToString());

        var result = await updater.UpdateAsync(command.Code, game => Place(game, command), cancellationToken);

        return result.Found ? result.Value : null;
    }

    // Runs again on fresh state after a conflict, so it only reads the game it is given. Placing a
    // ship where it already lies still saves: one write costs less than comparing for a no-op.
    private GameMutation<GameView?> Place(Game game, PlaceShipCommand command)
    {
        if (!game.IsPlayer(command.PlayerId))
        {
            return GameMutation.NoChange<GameView?>(null);
        }

        game.PlaceShip(command.PlayerId, command.Placement, time.GetUtcNow());
        return GameMutation.Save<GameView?>(GameViewMapper.ToView(game, command.PlayerId));
    }
}
