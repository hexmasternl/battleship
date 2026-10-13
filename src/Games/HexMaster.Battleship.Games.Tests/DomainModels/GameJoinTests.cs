using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Tests.Factories;

namespace HexMaster.Battleship.Games.Tests.DomainModels;

public class GameJoinTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    [Fact]
    public void Join_SeatsTheCallerAsPlayerTwoOfANewGame()
    {
        var game = GameFactory.Stored(GameState.New, withoutPlayerTwo: true, lastActivityOn: Now);
        var playerId = Guid.NewGuid();

        var outcome = game.Join(playerId, "Admiral Tromp", Later);

        Assert.Equal(JoinOutcome.Joined, outcome);
        Assert.Equal(GameState.New, game.GameState);
        Assert.Equal(DomainModelState.Modified, game.State);
        Assert.Equal(playerId, game.PlayerTwo!.PlayerId);
        Assert.Equal("Admiral Tromp", game.PlayerTwo.PlayerName);
        Assert.False(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerTwo.HasTurn);
        Assert.False(game.PlayerTwo.IsReady);
        Assert.Empty(game.PlayerTwo.Ships);
        Assert.Empty(game.PlayerTwo.Shots);
        Assert.True(game.IsPlayer(playerId));
    }

    [Fact]
    public void Join_TouchesTheGameAndTheJoiningPlayer()
    {
        var playerOne = GameFactory.Player(lastActivityOn: Now);
        var game = GameFactory.Stored(GameState.New, playerOne: playerOne, withoutPlayerTwo: true, lastActivityOn: Now);

        game.Join(Guid.NewGuid(), GameFactory.PlayerName(), Later);

        Assert.Equal(Later, game.LastActivityOn);
        Assert.Equal(Later, game.PlayerTwo!.LastActivityOn);
        Assert.Equal(Now, game.PlayerOne.LastActivityOn);
    }

    [Fact]
    public void Join_ByPlayerOneChangesNothing()
    {
        var playerOne = GameFactory.Player(lastActivityOn: Now);
        var game = GameFactory.Stored(GameState.New, playerOne: playerOne, withoutPlayerTwo: true, lastActivityOn: Now);

        var outcome = game.Join(playerOne.PlayerId, playerOne.PlayerName, Later);

        Assert.Equal(JoinOutcome.AlreadyPlayer, outcome);
        Assert.Null(game.PlayerTwo);
        Assert.Equal(DomainModelState.Pristine, game.State);
        Assert.Equal(Now, game.LastActivityOn);
        Assert.Equal(Now, game.PlayerOne.LastActivityOn);
    }

    [Theory]
    [InlineData(GameState.New)]
    [InlineData(GameState.Started)]
    [InlineData(GameState.Completed)]
    public void Join_ByPlayerTwoChangesNothingInAnyState(GameState state)
    {
        var playerTwo = GameFactory.Player(lastActivityOn: Now);
        var game = GameFactory.Stored(state, playerTwo: playerTwo, lastActivityOn: Now);

        var outcome = game.Join(playerTwo.PlayerId, playerTwo.PlayerName, Later);

        Assert.Equal(JoinOutcome.AlreadyPlayer, outcome);
        Assert.Same(playerTwo, game.PlayerTwo);
        Assert.Equal(DomainModelState.Pristine, game.State);
        Assert.Equal(Now, game.LastActivityOn);
    }

    [Theory]
    [InlineData(GameState.Started)]
    [InlineData(GameState.Completed)]
    [InlineData(GameState.Abandoned)]
    public void Join_RefusesAGameThatIsNoLongerNew(GameState state)
    {
        var game = GameFactory.Stored(state, lastActivityOn: Now);
        var playerTwo = game.PlayerTwo;

        var refused = Assert.Throws<GameJoinRefusedException>(() => game.Join(Guid.NewGuid(), GameFactory.PlayerName(), Later));

        Assert.Equal(GameJoinRefusedReason.NotJoinable, refused.Reason);
        Assert.Equal(game.GameCode, refused.GameCode);
        Assert.IsAssignableFrom<DomainException>(refused);
        Assert.Same(playerTwo, game.PlayerTwo);
        Assert.Equal(Now, game.LastActivityOn);
    }

    [Fact]
    public void Join_RefusesANewGameWhoseSecondSeatIsTaken()
    {
        var game = GameFactory.Stored(GameState.New, lastActivityOn: Now);
        var playerTwo = game.PlayerTwo;

        var refused = Assert.Throws<GameJoinRefusedException>(() => game.Join(Guid.NewGuid(), GameFactory.PlayerName(), Later));

        Assert.Equal(GameJoinRefusedReason.GameFull, refused.Reason);
        Assert.Same(playerTwo, game.PlayerTwo);
        Assert.Equal(DomainModelState.Pristine, game.State);
        Assert.Equal(Now, game.LastActivityOn);
    }

    [Fact]
    public void Join_RefusesAPlayerWithoutAName()
    {
        var game = GameFactory.Stored(GameState.New, withoutPlayerTwo: true);

        Assert.Throws<DomainException>(() => game.Join(Guid.NewGuid(), " ", Later));
        Assert.Null(game.PlayerTwo);
    }

    [Fact]
    public void IsPlayer_IsFalseForSomeoneElse()
    {
        var game = GameFactory.Stored();

        Assert.True(game.IsPlayer(game.PlayerOne.PlayerId));
        Assert.True(game.IsPlayer(game.PlayerTwo!.PlayerId));
        Assert.False(game.IsPlayer(Guid.NewGuid()));
    }
}
