using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;

namespace HexMaster.Battleship.Games.Tests.DomainModels;

public class GamePlacementTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(1);

    private static ShipPlacement Ship(ShipKind kind, int column, int row, Orientation orientation = Orientation.Horizontal) =>
        new(kind, new Coordinate(column, row), orientation);

    private static Game NewGame(Player? playerOne = null, Player? playerTwo = null, bool withoutPlayerTwo = false) =>
        GameFactory.Stored(GameState.New, playerOne, playerTwo, withoutPlayerTwo, Now);

    private static PlacementRefusedReason Refusal(Action act) => Assert.Throws<PlacementRefusedException>(act).Reason;

    [Fact]
    public void PlaceShip_AddsTheFirstPlacementAndTouchesTheGameAndPlayer()
    {
        var game = NewGame();
        var playerId = game.PlayerOne.PlayerId;

        game.PlaceShip(playerId, Ship(ShipKind.Carrier, 0, 0), Later);

        Assert.Equal([Ship(ShipKind.Carrier, 0, 0)], game.PlayerOne.Ships);
        Assert.Empty(game.PlayerTwo!.Ships);
        Assert.Equal(Later, game.LastActivityOn);
        Assert.Equal(Later, game.PlayerOne.LastActivityOn);
        Assert.Equal(DomainModelState.Modified, game.State);
    }

    [Fact]
    public void PlaceShip_MovesAShipThatIsAlreadyPlaced()
    {
        var game = NewGame();
        var playerId = game.PlayerOne.PlayerId;
        game.PlaceShip(playerId, Ship(ShipKind.Cruiser, 0, 0), Now);

        game.PlaceShip(playerId, Ship(ShipKind.Cruiser, 5, 5, Orientation.Vertical), Later);

        Assert.Equal([Ship(ShipKind.Cruiser, 5, 5, Orientation.Vertical)], game.PlayerOne.Ships);
    }

    [Fact]
    public void PlaceShip_RotatesInPlace()
    {
        var game = NewGame();
        var playerId = game.PlayerOne.PlayerId;
        game.PlaceShip(playerId, Ship(ShipKind.Battleship, 3, 3), Now);

        game.PlaceShip(playerId, Ship(ShipKind.Battleship, 3, 3, Orientation.Vertical), Later);

        Assert.Equal([Ship(ShipKind.Battleship, 3, 3, Orientation.Vertical)], game.PlayerOne.Ships);
    }

    [Fact]
    public void PlaceShip_AllowsShipsThatTouch()
    {
        var game = NewGame();
        var playerId = game.PlayerOne.PlayerId;
        game.PlaceShip(playerId, Ship(ShipKind.Carrier, 0, 0), Now);

        game.PlaceShip(playerId, Ship(ShipKind.Destroyer, 0, 1), Later);
        game.PlaceShip(playerId, Ship(ShipKind.Submarine, 5, 0), Later);

        Assert.Equal(3, game.PlayerOne.Ships.Count);
    }

    [Fact]
    public void PlaceShip_LetsAShipMoveOntoItsOwnCells()
    {
        var game = NewGame();
        var playerId = game.PlayerOne.PlayerId;
        game.PlaceShip(playerId, Ship(ShipKind.Carrier, 0, 0), Now);

        game.PlaceShip(playerId, Ship(ShipKind.Carrier, 2, 0), Later);

        Assert.Equal([Ship(ShipKind.Carrier, 2, 0)], game.PlayerOne.Ships);
    }

    [Fact]
    public void PlaceShip_WorksWhileAloneInTheGame()
    {
        var game = NewGame(withoutPlayerTwo: true);

        game.PlaceShip(game.PlayerOne.PlayerId, Ship(ShipKind.Destroyer, 10, 11), Later);

        Assert.Single(game.PlayerOne.Ships);
    }

    [Theory]
    [InlineData(8, 0, Orientation.Horizontal)]
    [InlineData(0, 8, Orientation.Vertical)]
    [InlineData(-1, 0, Orientation.Horizontal)]
    [InlineData(0, 12, Orientation.Horizontal)]
    public void PlaceShip_RefusesAShipOffTheBoard(int column, int row, Orientation orientation)
    {
        var game = NewGame();

        var reason = Refusal(() => game.PlaceShip(game.PlayerOne.PlayerId, Ship(ShipKind.Carrier, column, row, orientation), Later));

        Assert.Equal(PlacementRefusedReason.OutOfBoard, reason);
        Assert.Empty(game.PlayerOne.Ships);
        Assert.Equal(Now, game.LastActivityOn);
    }

    [Fact]
    public void PlaceShip_RefusesAnOverlapAndKeepsTheFleet()
    {
        var game = NewGame();
        var playerId = game.PlayerOne.PlayerId;
        game.PlaceShip(playerId, Ship(ShipKind.Carrier, 0, 0), Now);

        var refused = Assert.Throws<PlacementRefusedException>(
            () => game.PlaceShip(playerId, Ship(ShipKind.Cruiser, 2, 0, Orientation.Vertical), Later));

        Assert.Equal(PlacementRefusedReason.Overlap, refused.Reason);
        Assert.Contains("Carrier", refused.Message, StringComparison.Ordinal);
        Assert.Equal([Ship(ShipKind.Carrier, 0, 0)], game.PlayerOne.Ships);
    }

    [Fact]
    public void PlaceShip_IgnoresTheOpponentsShips()
    {
        var game = NewGame(playerTwo: GameFactory.Player(ships: [Ship(ShipKind.Carrier, 0, 0)]));

        game.PlaceShip(game.PlayerOne.PlayerId, Ship(ShipKind.Carrier, 0, 0), Later);

        Assert.Single(game.PlayerOne.Ships);
    }

    [Fact]
    public void PlaceShip_RefusesAReadyPlayer()
    {
        var game = NewGame(playerOne: GameFactory.Player(isReady: true, ships: GameFactory.FullFleet()));

        Assert.Equal(PlacementRefusedReason.PlayerReady,
            Refusal(() => game.PlaceShip(game.PlayerOne.PlayerId, Ship(ShipKind.Carrier, 0, 11), Later)));
    }

    [Theory]
    [InlineData(GameState.Started)]
    [InlineData(GameState.Completed)]
    [InlineData(GameState.Abandoned)]
    public void PlaceShip_RefusesAGameThatIsNotNew(GameState state)
    {
        // Checked before readiness and bounds: a started game refuses even a ready player's off-board ship.
        var game = GameFactory.Stored(state, playerOne: GameFactory.Player(isReady: true));

        Assert.Equal(PlacementRefusedReason.GameAlreadyStarted,
            Refusal(() => game.PlaceShip(game.PlayerOne.PlayerId, Ship(ShipKind.Carrier, 11, 11), Later)));
    }

    [Fact]
    public void PlaceShip_RefusesSomeoneWhoDoesNotPlay()
    {
        var game = NewGame();

        Assert.Throws<DomainException>(() => game.PlaceShip(Guid.NewGuid(), Ship(ShipKind.Carrier, 0, 0), Later));
    }

    [Fact]
    public void MarkReady_MakesAPlayerWithTheFullFleetReadyAndKeepsTheGameNewWhileAlone()
    {
        var game = NewGame(playerOne: GameFactory.Player(ships: GameFactory.FullFleet()), withoutPlayerTwo: true);

        var outcome = game.MarkReady(game.PlayerOne.PlayerId, new FixedPicker(), Later);

        Assert.Equal(ReadyOutcome.Ready, outcome);
        Assert.True(game.PlayerOne.IsReady);
        Assert.Equal(GameState.New, game.GameState);
        Assert.Equal(Later, game.PlayerOne.LastActivityOn);
    }

    [Fact]
    public void MarkReady_WaitsForAnOpponentWhoIsNotReady()
    {
        var game = NewGame(playerOne: GameFactory.Player(ships: GameFactory.FullFleet()));

        Assert.Equal(ReadyOutcome.Ready, game.MarkReady(game.PlayerOne.PlayerId, new FixedPicker(), Later));
        Assert.Equal(GameState.New, game.GameState);
        Assert.False(game.PlayerOne.HasTurn);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MarkReady_StartsTheGameWhenBothAreReadyAndGivesThePickedPlayerTheTurn(bool pickPlayerOne)
    {
        var game = NewGame(
            playerOne: GameFactory.Player(isReady: true, ships: GameFactory.FullFleet()),
            playerTwo: GameFactory.Player(ships: GameFactory.FullFleet()));
        var picker = new FixedPicker(pickPlayerOne);

        var outcome = game.MarkReady(game.PlayerTwo!.PlayerId, picker, Later);

        Assert.Equal(ReadyOutcome.ReadyAndStarted, outcome);
        Assert.Equal(GameState.Started, game.GameState);
        Assert.Equal(pickPlayerOne, game.PlayerOne.HasTurn);
        Assert.Equal(!pickPlayerOne, game.PlayerTwo.HasTurn);
        Assert.Equal((game.PlayerOne.PlayerId, game.PlayerTwo.PlayerId), picker.Asked);
    }

    [Fact]
    public void MarkReady_IsHarmlessWhenAlreadyReadyInAnyState()
    {
        var game = GameFactory.Stored(
            GameState.Started,
            playerOne: GameFactory.Player(isReady: true, hasTurn: true, ships: GameFactory.FullFleet()),
            lastActivityOn: Now);

        var outcome = game.MarkReady(game.PlayerOne.PlayerId, new FixedPicker(), Later);

        Assert.Equal(ReadyOutcome.AlreadyReady, outcome);
        Assert.Equal(DomainModelState.Pristine, game.State);
        Assert.Equal(Now, game.LastActivityOn);
    }

    [Fact]
    public void MarkReady_RefusesAnIncompleteFleet()
    {
        var game = NewGame(playerOne: GameFactory.Player(ships: GameFactory.FullFleet().Take(4)));

        Assert.Equal(PlacementRefusedReason.FleetIncomplete,
            Refusal(() => game.MarkReady(game.PlayerOne.PlayerId, new FixedPicker(), Later)));
        Assert.False(game.PlayerOne.IsReady);
    }

    [Fact]
    public void MarkReady_RefusesAGameThatIsNotNew()
    {
        var game = GameFactory.Stored(GameState.Completed, playerOne: GameFactory.Player(ships: GameFactory.FullFleet()));

        Assert.Equal(PlacementRefusedReason.GameAlreadyStarted,
            Refusal(() => game.MarkReady(game.PlayerOne.PlayerId, new FixedPicker(), Later)));
    }

    [Fact]
    public void RandomFirstTurnPicker_PicksBothPlayersOverManyDraws()
    {
        var picker = new RandomFirstTurnPicker();
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();

        var picks = Enumerable.Range(0, 200).Select(_ => picker.Pick(one, two)).ToHashSet();

        Assert.Equal([one, two], picks.OrderBy(p => p == two));
    }

    // Picks player one or player two, and remembers what it was asked.
    private sealed class FixedPicker(bool pickPlayerOne = true) : IFirstTurnPicker
    {
        public (Guid, Guid)? Asked { get; private set; }

        public Guid Pick(Guid playerOneId, Guid playerTwoId)
        {
            Asked = (playerOneId, playerTwoId);
            return pickPlayerOne ? playerOneId : playerTwoId;
        }
    }
}
