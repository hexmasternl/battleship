using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Tests.Factories;

namespace HexMaster.Battleship.Games.Tests.DomainModels;

public class GameTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    [Fact]
    public void Create_StartsNewWithTheCreatorAsPlayerOne()
    {
        var playerId = Guid.NewGuid();
        var code = GameFactory.Code();

        var game = Game.Create(playerId, "Admiral Nelson", code, Now);

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal(code, game.Code);
        Assert.Equal(code.Value, game.GameCode);
        Assert.Equal(GameState.New, game.GameState);
        Assert.Equal(DomainModelState.New, game.State);
        Assert.Equal(playerId, game.PlayerOne.PlayerId);
        Assert.Equal("Admiral Nelson", game.PlayerOne.PlayerName);
        Assert.False(game.PlayerOne.IsReady);
        Assert.Empty(game.PlayerOne.Ships);
        Assert.Empty(game.PlayerOne.Shots);
        Assert.Null(game.PlayerTwo);
    }

    [Fact]
    public void Create_GivesNobodyTheTurn()
    {
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);

        Assert.False(game.PlayerOne.HasTurn);
    }

    [Fact]
    public void Create_SetsActivityOnTheGameAndPlayerOne()
    {
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);

        Assert.Equal(Now, game.LastActivityOn);
        Assert.Equal(Now, game.PlayerOne.LastActivityOn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RefusesAPlayerWithoutAName(string name)
    {
        Assert.Throws<DomainException>(() => Game.Create(Guid.NewGuid(), name, GameFactory.Code(), Now));
    }

    [Fact]
    public void Create_RefusesAPlayerWithoutAnId()
    {
        Assert.Throws<DomainException>(() => Game.Create(Guid.Empty, GameFactory.PlayerName(), GameFactory.Code(), Now));
    }

    [Fact]
    public void Create_RefusesAnEmptyCode()
    {
        Assert.Throws<DomainException>(() => Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), default, Now));
    }

    [Fact]
    public void Create_TrimsThePlayerName()
    {
        var game = Game.Create(Guid.NewGuid(), "  Admiral Nelson ", GameFactory.Code(), Now);

        Assert.Equal("Admiral Nelson", game.PlayerOne.PlayerName);
    }

    [Fact]
    public void GiveFirstTurnTo_GivesTheTurnToThatPlayerOnly()
    {
        var game = GameFactory.Stored();

        game.GiveFirstTurnTo(game.PlayerTwo!.PlayerId, Later);

        Assert.True(game.PlayerTwo.HasTurn);
        Assert.False(game.PlayerOne.HasTurn);
        Assert.Equal(DomainModelState.Modified, game.State);
    }

    [Fact]
    public void GiveFirstTurnTo_RecordsActivityOnTheGameAndThatPlayer()
    {
        var game = GameFactory.Stored();
        var playerOneActivity = game.PlayerOne.LastActivityOn;

        game.GiveFirstTurnTo(game.PlayerTwo!.PlayerId, Later);

        Assert.Equal(Later, game.LastActivityOn);
        Assert.Equal(Later, game.PlayerTwo.LastActivityOn);
        Assert.Equal(playerOneActivity, game.PlayerOne.LastActivityOn);
    }

    [Fact]
    public void GiveFirstTurnTo_IsRefusedWhenTheOtherPlayerHasTheTurn()
    {
        var game = GameFactory.Stored(playerOne: GameFactory.Player(hasTurn: true));

        Assert.Throws<DomainException>(() => game.GiveFirstTurnTo(game.PlayerTwo!.PlayerId, Later));

        Assert.True(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerTwo!.HasTurn);
        Assert.Equal(DomainModelState.Pristine, game.State);
    }

    [Fact]
    public void GiveFirstTurnTo_IsRefusedWhileNew()
    {
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);

        Assert.Throws<DomainException>(() => game.GiveFirstTurnTo(game.PlayerOne.PlayerId, Later));

        Assert.False(game.PlayerOne.HasTurn);
    }

    [Theory]
    [InlineData(GameState.Completed)]
    [InlineData(GameState.Abandoned)]
    public void GiveFirstTurnTo_IsRefusedOnceTheGameIsOver(GameState gameState)
    {
        var game = GameFactory.Stored(gameState);

        Assert.Throws<DomainException>(() => game.GiveFirstTurnTo(game.PlayerOne.PlayerId, Later));
    }

    [Fact]
    public void GiveFirstTurnTo_IsRefusedForAPlayerOutsideTheGame()
    {
        var game = GameFactory.Stored();

        Assert.Throws<DomainException>(() => game.GiveFirstTurnTo(Guid.NewGuid(), Later));
    }

    [Fact]
    public void SwitchTurn_FlipsBothPlayers()
    {
        var game = GameFactory.Stored(playerOne: GameFactory.Player(hasTurn: true));

        game.SwitchTurn(Later);

        Assert.False(game.PlayerOne.HasTurn);
        Assert.True(game.PlayerTwo!.HasTurn);
        Assert.Equal(DomainModelState.Modified, game.State);
    }

    [Fact]
    public void SwitchTurn_FlipsBackAgain()
    {
        var game = GameFactory.Stored(playerTwo: GameFactory.Player(hasTurn: true));

        game.SwitchTurn(Later);

        Assert.True(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerTwo!.HasTurn);
    }

    [Fact]
    public void SwitchTurn_RecordsActivityOnTheGameAndThePlayerWhoHadTheTurn()
    {
        var game = GameFactory.Stored(playerOne: GameFactory.Player(hasTurn: true));

        game.SwitchTurn(Later);

        Assert.Equal(Later, game.LastActivityOn);
        Assert.Equal(Later, game.PlayerOne.LastActivityOn);
    }

    [Fact]
    public void SwitchTurn_IsRefusedWhenNobodyHasTheTurn()
    {
        var game = GameFactory.Stored();

        Assert.Throws<DomainException>(() => game.SwitchTurn(Later));

        Assert.False(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerTwo!.HasTurn);
    }

    [Fact]
    public void SwitchTurn_IsRefusedWithoutASecondPlayer()
    {
        var game = GameFactory.Stored(playerOne: GameFactory.Player(hasTurn: true), withoutPlayerTwo: true);

        Assert.Throws<DomainException>(() => game.SwitchTurn(Later));
    }

    [Fact]
    public void SwitchTurn_IsRefusedWhileNew()
    {
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);

        Assert.Throws<DomainException>(() => game.SwitchTurn(Later));
    }

    [Fact]
    public void Reconcile_RefusesTwoPlayersWithTheTurn()
    {
        Assert.Throws<DomainException>(() => GameFactory.Stored(
            playerOne: GameFactory.Player(hasTurn: true),
            playerTwo: GameFactory.Player(hasTurn: true)));
    }

    [Fact]
    public void Reconcile_RefusesANewGameWithATurn()
    {
        Assert.Throws<DomainException>(() => GameFactory.Stored(
            GameState.New,
            playerOne: GameFactory.Player(hasTurn: true),
            withoutPlayerTwo: true));
    }

    [Fact]
    public void Reconcile_RestoresEveryValueAsPristine()
    {
        var id = Guid.NewGuid();
        var code = GameFactory.Code();
        var ships = GameFactory.FullFleet();
        var shots = new[] { GameFactory.Shot(hit: true), GameFactory.Shot(hit: false) };
        var playerOne = GameFactory.Player(hasTurn: true, isReady: true, ships: ships, shots: shots, lastActivityOn: Now);
        var playerTwo = GameFactory.Player(lastActivityOn: Later);

        var game = Game.Reconcile(id, code, GameState.Started, playerOne, playerTwo, Later);

        Assert.Equal(DomainModelState.Pristine, game.State);
        Assert.Equal(id, game.Id);
        Assert.Equal(code, game.Code);
        Assert.Equal(GameState.Started, game.GameState);
        Assert.Equal(Later, game.LastActivityOn);
        Assert.Same(playerOne, game.PlayerOne);
        Assert.Same(playerTwo, game.PlayerTwo);
        Assert.True(game.PlayerOne.IsReady);
        Assert.Equal(ships, game.PlayerOne.Ships);
        Assert.Equal(shots, game.PlayerOne.Shots);
        Assert.Equal(Now, game.PlayerOne.LastActivityOn);
        Assert.Null(game.WinnerId);
    }

    [Fact]
    public void Reconcile_RestoresACompletedGameWithItsWinner()
    {
        var playerOne = GameFactory.Player();
        var playerTwo = GameFactory.Player();

        var game = GameFactory.Stored(GameState.Completed, playerOne, playerTwo, winnerId: playerTwo.PlayerId);

        Assert.Equal(DomainModelState.Pristine, game.State);
        Assert.Equal(GameState.Completed, game.GameState);
        Assert.Equal(playerTwo.PlayerId, game.WinnerId);
        Assert.False(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerTwo!.HasTurn);
    }

    [Theory]
    [InlineData(GameState.New)]
    [InlineData(GameState.Started)]
    public void RunningGames_HaveNoWinner(GameState gameState)
    {
        Assert.Null(GameFactory.Stored(gameState).WinnerId);
        Assert.Null(Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now).WinnerId);
    }

    [Theory]
    [InlineData(GameState.New)]
    [InlineData(GameState.Started)]
    public void Reconcile_RefusesAWinnerOfARunningGame(GameState gameState)
    {
        var playerOne = GameFactory.Player();

        Assert.Throws<DomainException>(() => GameFactory.Stored(gameState, playerOne, winnerId: playerOne.PlayerId));
    }

    [Fact]
    public void Reconcile_RefusesAWinnerWhoDoesNotPlay()
    {
        Assert.Throws<DomainException>(() => GameFactory.Stored(GameState.Completed, winnerId: Guid.NewGuid()));
    }

    [Fact]
    public void Reconcile_RefusesACompletedGameWithATurn()
    {
        var playerOne = GameFactory.Player(hasTurn: true);

        Assert.Throws<DomainException>(() => GameFactory.Stored(GameState.Completed, playerOne, winnerId: playerOne.PlayerId));
    }

    [Fact]
    public void Player_ReconcileCopiesTheShipsAndShots()
    {
        var ships = new List<ShipPlacement> { GameFactory.FullFleet()[0] };
        var shots = new List<Shot> { GameFactory.Shot() };

        var player = GameFactory.Player(ships: ships, shots: shots);
        ships.Add(GameFactory.FullFleet()[1]);
        shots.Add(GameFactory.Shot());

        Assert.Single(player.Ships);
        Assert.Single(player.Shots);
    }

    [Fact]
    public void Player_RefusesTwoShipsOfTheSameKind()
    {
        var carrier = GameFactory.FullFleet()[0];

        Assert.Throws<DomainException>(() => GameFactory.Player(ships: [carrier, carrier with { Origin = new(0, 11) }]));
    }

    [Fact]
    public void DeletedGame_CannotChange()
    {
        var game = GameFactory.Stored(playerOne: GameFactory.Player(hasTurn: true));
        game.Delete();

        Assert.Throws<DomainException>(() => game.SwitchTurn(Later));
    }
}
