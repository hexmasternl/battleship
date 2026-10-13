using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Features.GetGame;

// The only producer of GameView, so the rule that the opponent's ships never leave the server
// cannot be bypassed by a later endpoint.
public static class GameViewMapper
{
    // The caller must be a player of the game; handlers check that before mapping.
    public static GameView ToView(IGame game, Guid callerId)
    {
        ArgumentNullException.ThrowIfNull(game);

        var (me, opponent) = game.PlayerOne.PlayerId == callerId ? (game.PlayerOne, game.PlayerTwo)
            : game.PlayerTwo?.PlayerId == callerId ? (game.PlayerTwo, game.PlayerOne)
            : throw new ArgumentException($"Player {callerId} does not play in game {game.GameCode}.", nameof(callerId));

        return new GameView(
            game.Id,
            game.GameCode,
            game.GameState,
            new PlayerView(
                me.PlayerId,
                me.PlayerName,
                me.IsReady,
                me.HasTurn,
                [.. me.Ships.Select(s => new ShipView(s.Kind, s.Origin.Column, s.Origin.Row, s.Orientation))],
                Shots(me)),
            opponent is null
                ? null
                : new OpponentView(opponent.PlayerId, opponent.PlayerName, opponent.IsReady, opponent.HasTurn, Shots(opponent)),
            game.WinnerId);
    }

    private static IReadOnlyList<ShotView> Shots(IPlayer player) =>
        [.. player.Shots.Select(s => new ShotView(s.Column, s.Row, s.Hit))];
}
