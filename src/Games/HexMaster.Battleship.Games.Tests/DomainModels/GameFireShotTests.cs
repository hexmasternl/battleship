using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Tests.Factories;

namespace HexMaster.Battleship.Games.Tests.DomainModels;

public class GameFireShotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(1);

    // The full fleet lies on the even rows from column 0: the carrier covers (0..4, 0).
    private static readonly Coordinate CarrierCell = new(2, 0);
    private static readonly Coordinate Water = new(11, 11);

    // A started game in which player one has the turn and both fleets are placed.
    private static Game Started(GameState gameState = GameState.Started, IEnumerable<Shot>? playerOneShots = null) =>
        GameFactory.Stored(
            gameState,
            GameFactory.Player(hasTurn: true, isReady: true, ships: GameFactory.FullFleet(), shots: playerOneShots, lastActivityOn: Now),
            GameFactory.Player(isReady: true, ships: GameFactory.FullFleet(), lastActivityOn: Now),
            lastActivityOn: Now);

    // The last cell of the patrol boat, the final ship of the full fleet.
    private static readonly Coordinate LastCell = GameFactory.FullFleet()[^1].Cells[^1];

    // A hit on every cell of the full fleet except the given one.
    private static IEnumerable<Shot> AllFleetHitsExcept(Coordinate except) =>
        GameFactory.FullFleet().SelectMany(s => s.Cells).Where(c => c != except).Select(c => new Shot(c.Column, c.Row, true));

    // A game player one won by hitting the whole fleet of player two.
    private static Game Completed()
    {
        var winner = GameFactory.Player(
            isReady: true, ships: GameFactory.FullFleet(), shots: AllFleetHitsExcept(new Coordinate(-1, -1)), lastActivityOn: Now);

        return GameFactory.Stored(
            GameState.Completed,
            winner,
            GameFactory.Player(isReady: true, ships: GameFactory.FullFleet(), lastActivityOn: Now),
            lastActivityOn: Now,
            winnerId: winner.PlayerId);
    }

    private static ShotRefusedReason Refusal(Action act) => Assert.Throws<ShotRefusedException>(act).Reason;

    [Fact]
    public void FireShot_OnAShipCell_RecordsAHit()
    {
        var game = Started();

        var outcome = game.FireShot(game.PlayerOne.PlayerId, CarrierCell, Later);

        Assert.Equal(new ShotOutcome(CarrierCell, true), outcome);
        Assert.Equal([new Shot(CarrierCell.Column, CarrierCell.Row, true)], game.PlayerOne.Shots);
        Assert.Empty(game.PlayerTwo!.Shots);
    }

    [Fact]
    public void FireShot_OnWater_RecordsAMiss()
    {
        var game = Started();

        var outcome = game.FireShot(game.PlayerOne.PlayerId, Water, Later);

        Assert.False(outcome.Hit);
        Assert.Equal([new Shot(Water.Column, Water.Row, false)], game.PlayerOne.Shots);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(11, 11)]
    public void FireShot_HitOrMiss_PassesTheTurnAndTouchesTheShooter(int column, int row)
    {
        var game = Started();

        game.FireShot(game.PlayerOne.PlayerId, new Coordinate(column, row), Later);

        Assert.False(game.PlayerOne.HasTurn);
        Assert.True(game.PlayerTwo!.HasTurn);
        Assert.Equal(Later, game.LastActivityOn);
        Assert.Equal(Later, game.PlayerOne.LastActivityOn);
        Assert.Equal(Now, game.PlayerTwo.LastActivityOn);
        Assert.Equal(DomainModelState.Modified, game.State);
    }

    [Theory]
    [InlineData(5, 2, true)]
    [InlineData(0, 0, false)]
    public void FireShot_JudgesAgainstTheOpponentsShipsOnly(int column, int row, bool expectedHit)
    {
        // The opponent only has a destroyer on (4..5, 2); (0, 0) is under the shooter's own carrier.
        var game = GameFactory.Stored(
            GameState.Started,
            GameFactory.Player(hasTurn: true, ships: GameFactory.FullFleet()),
            GameFactory.Player(ships: [new ShipPlacement(ShipKind.Destroyer, new Coordinate(4, 2), Orientation.Horizontal)]));

        var outcome = game.FireShot(game.PlayerOne.PlayerId, new Coordinate(column, row), Later);

        Assert.Equal(expectedHit, outcome.Hit);
    }

    [Fact]
    public void FireShot_BothPlayersMayFireAtTheSameCoordinates()
    {
        var game = Started();

        game.FireShot(game.PlayerOne.PlayerId, CarrierCell, Now);
        var outcome = game.FireShot(game.PlayerTwo!.PlayerId, CarrierCell, Later);

        Assert.True(outcome.Hit);
        Assert.Single(game.PlayerOne.Shots);
        Assert.Single(game.PlayerTwo.Shots);
        Assert.True(game.PlayerOne.HasTurn);
    }

    [Fact]
    public void FireShot_WhenNew_IsRefusedWithoutChange()
    {
        var game = GameFactory.Stored(
            GameState.New,
            GameFactory.Player(ships: GameFactory.FullFleet(), lastActivityOn: Now),
            GameFactory.Player(ships: GameFactory.FullFleet(), lastActivityOn: Now),
            lastActivityOn: Now);

        Assert.Equal(ShotRefusedReason.GameNotStarted, Refusal(() => game.FireShot(game.PlayerOne.PlayerId, Water, Later)));
        AssertUnchanged(game);
    }

    [Fact]
    public void FireShot_OnTheLastUnhitShipCell_CompletesTheGameWithTheShooterAsWinner()
    {
        var game = Started(playerOneShots: AllFleetHitsExcept(LastCell));

        var outcome = game.FireShot(game.PlayerOne.PlayerId, LastCell, Later);

        Assert.Equal(new ShotOutcome(LastCell, true, true), outcome);
        Assert.Equal(GameState.Completed, game.GameState);
        Assert.Equal(game.PlayerOne.PlayerId, game.WinnerId);
        Assert.False(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerTwo!.HasTurn);
        Assert.Contains(new Shot(LastCell.Column, LastCell.Row, true), game.PlayerOne.Shots);
        Assert.Equal(Later, game.LastActivityOn);
        Assert.Equal(Later, game.PlayerOne.LastActivityOn);
        Assert.Equal(Now, game.PlayerTwo.LastActivityOn);
        Assert.Equal(DomainModelState.Modified, game.State);
    }

    [Fact]
    public void FireShot_HitWhileAnotherShipCellIsUnhit_KeepsPlayingAndPassesTheTurn()
    {
        // Two cells left: the carrier cell and the last cell; hitting the carrier cell leaves one.
        var remaining = AllFleetHitsExcept(LastCell).Where(s => s != new Shot(CarrierCell.Column, CarrierCell.Row, true));
        var game = Started(playerOneShots: remaining);

        var outcome = game.FireShot(game.PlayerOne.PlayerId, CarrierCell, Later);

        Assert.True(outcome.Hit);
        Assert.False(outcome.GameCompleted);
        Assert.Equal(GameState.Started, game.GameState);
        Assert.Null(game.WinnerId);
        Assert.True(game.PlayerTwo!.HasTurn);
    }

    [Fact]
    public void FireShot_MissWithOneShipCellLeft_NeverEndsTheGame()
    {
        var game = Started(playerOneShots: AllFleetHitsExcept(LastCell));

        var outcome = game.FireShot(game.PlayerOne.PlayerId, Water, Later);

        Assert.False(outcome.GameCompleted);
        Assert.Equal(GameState.Started, game.GameState);
        Assert.Null(game.WinnerId);
        Assert.True(game.PlayerTwo!.HasTurn);
    }

    [Fact]
    public void FireShot_MissesDoNotCountAsHitsOfAShipCell()
    {
        // The last cell was shot before but recorded as a miss; it still has to be hit.
        var shots = AllFleetHitsExcept(LastCell).Append(new Shot(LastCell.Column, LastCell.Row, false));
        var game = Started(playerOneShots: shots);

        var outcome = game.FireShot(game.PlayerOne.PlayerId, Water, Later);

        Assert.False(outcome.GameCompleted);
        Assert.Equal(GameState.Started, game.GameState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FireShot_OnACompletedGame_IsRefusedForWinnerAndLoserWithoutChange(bool winnerFires)
    {
        var game = Completed();
        var shooter = winnerFires ? game.PlayerOne.PlayerId : game.PlayerTwo!.PlayerId;

        Assert.Equal(ShotRefusedReason.GameNotStarted, Refusal(() => game.FireShot(shooter, Water, Later)));
        AssertUnchanged(game);
        Assert.Equal(GameState.Completed, game.GameState);
        Assert.Equal(game.PlayerOne.PlayerId, game.WinnerId);
        Assert.False(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerTwo!.HasTurn);
        Assert.Equal(17, game.PlayerOne.Shots.Count);
    }

    [Fact]
    public void FireShot_WithoutTheTurn_IsRefusedWithoutChange()
    {
        var game = Started();

        Assert.Equal(ShotRefusedReason.NotYourTurn, Refusal(() => game.FireShot(game.PlayerTwo!.PlayerId, Water, Later)));
        AssertUnchanged(game);
        Assert.True(game.PlayerOne.HasTurn);
    }

    [Fact]
    public void FireShot_AtARepeatedCell_IsRefusedAndKeepsTheTurn()
    {
        var game = Started(playerOneShots: [new Shot(3, 7, false)]);

        Assert.Equal(ShotRefusedReason.CellAlreadyShot, Refusal(() => game.FireShot(game.PlayerOne.PlayerId, new Coordinate(3, 7), Later)));
        AssertUnchanged(game);
        Assert.True(game.PlayerOne.HasTurn);
        Assert.Single(game.PlayerOne.Shots);
    }

    [Fact]
    public void FireShot_OffTheBoard_IsRefusedWithoutChange()
    {
        var game = Started();

        Assert.Throws<DomainException>(() => game.FireShot(game.PlayerOne.PlayerId, new Coordinate(12, 3), Later));
        AssertUnchanged(game);
    }

    [Fact]
    public void FireShot_ByANonPlayer_IsRefused()
    {
        var game = Started();

        Assert.Throws<DomainException>(() => game.FireShot(Guid.NewGuid(), Water, Later));
        AssertUnchanged(game);
    }

    [Fact]
    public void FireShot_RefusesTheStateBeforeTheTurn()
    {
        var game = GameFactory.Stored(GameState.New, GameFactory.Player(), GameFactory.Player());

        Assert.Equal(ShotRefusedReason.GameNotStarted, Refusal(() => game.FireShot(game.PlayerTwo!.PlayerId, Water, Later)));
    }

    private static void AssertUnchanged(Game game)
    {
        Assert.Equal(DomainModelState.Pristine, game.State);
        Assert.Equal(Now, game.LastActivityOn);
        Assert.Equal(Now, game.PlayerOne.LastActivityOn);
        Assert.Equal(Now, game.PlayerTwo!.LastActivityOn);
    }
}
