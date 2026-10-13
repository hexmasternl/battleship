using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Data.Entities;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Tests.Factories;

namespace HexMaster.Battleship.Games.Tests.Storage;

// The JSON document on its own, so the shape is checked without table storage.
public class GameDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Document_RoundTripsAGame()
    {
        var playerOne = GameFactory.Player(
            hasTurn: true, isReady: true, ships: GameFactory.FullFleet(), shots: [new(3, 4, true)], lastActivityOn: Now);
        var playerTwo = GameFactory.Player(lastActivityOn: Now.AddSeconds(5));
        var game = Game.Reconcile(Guid.NewGuid(), GameFactory.Code(), GameState.Started, playerOne, playerTwo, Now.AddSeconds(5));

        var json = GameDocument.From(game).Serialize();
        var restored = GameDocument.Deserialize(json, GameDocument.CurrentVersion).ToGame();

        Assert.Equal(game.Id, restored.Id);
        Assert.Equal(game.Code, restored.Code);
        Assert.Equal(game.GameState, restored.GameState);
        Assert.Equal(game.LastActivityOn, restored.LastActivityOn);
        Assert.Equal(game.PlayerOne.PlayerId, restored.PlayerOne.PlayerId);
        Assert.Equal(game.PlayerOne.PlayerName, restored.PlayerOne.PlayerName);
        Assert.True(restored.PlayerOne.HasTurn);
        Assert.Equal(Now, restored.PlayerOne.LastActivityOn);
        Assert.True(restored.PlayerOne.IsReady);
        Assert.Equal(game.PlayerOne.Ships, restored.PlayerOne.Ships);
        Assert.Equal(game.PlayerOne.Shots, restored.PlayerOne.Shots);
        Assert.Equal(playerTwo.PlayerId, restored.PlayerTwo!.PlayerId);
    }

    [Fact]
    public void Document_WritesCamelCaseAndTheStateByName()
    {
        var game = Game.Create(Guid.NewGuid(), "Admiral Nelson", GameFactory.Code(), Now);

        var json = GameDocument.From(game).Serialize();

        Assert.Contains("\"gameState\":\"New\"", json);
        Assert.Contains("\"playerName\":\"Admiral Nelson\"", json);
        Assert.Contains("\"playerTwo\":null", json);
    }

    [Fact]
    public void Deserialize_RefusesAnUnknownVersion()
    {
        Assert.Throws<NotSupportedException>(() => GameDocument.Deserialize("{}", GameDocument.CurrentVersion + 1));
    }

    [Fact]
    public void Deserialize_RefusesAnEmptyDocument()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => GameDocument.Deserialize("null", GameDocument.CurrentVersion));
    }
}
