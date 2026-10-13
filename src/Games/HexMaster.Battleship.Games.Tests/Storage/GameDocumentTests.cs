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
    public void Document_WritesShipsAndShotsByName()
    {
        var game = Game.Create(Guid.NewGuid(), "Admiral Nelson", GameFactory.Code(), Now);
        game.PlaceShip(game.PlayerOne.PlayerId, new ShipPlacement(ShipKind.Cruiser, new(2, 3), Orientation.Vertical), Now);

        var json = GameDocument.From(game).Serialize();

        Assert.Equal(2, GameDocument.CurrentVersion);
        Assert.Contains("\"ships\":[{\"kind\":\"Cruiser\",\"column\":2,\"row\":3,\"orientation\":\"Vertical\"}]", json);
        Assert.Contains("\"isReady\":false", json);
        Assert.Contains("\"shots\":[]", json);
    }

    [Fact]
    public void VersionOne_IsUpgradedKeepingIdentityStateAndActivity()
    {
        var id = Guid.NewGuid();
        var playerOne = Guid.NewGuid();
        var playerTwo = Guid.NewGuid();
        var json = $$"""
            {"id":"{{id}}","gameCode":"K7Q2M9XA","gameState":"Started",
             "playerOne":{"playerId":"{{playerOne}}","playerName":"Admiral Nelson","hasTurn":true,
               "shipCoordinates":[{"column":1,"row":1}],"shotCoordinates":[{"column":3,"row":4},{"column":5,"row":6}],
               "lastActivityOn":"{{Now:O}}"},
             "playerTwo":{"playerId":"{{playerTwo}}","playerName":"Admiral Tromp","hasTurn":false,
               "shipCoordinates":[],"shotCoordinates":[],"lastActivityOn":"{{Now.AddSeconds(5):O}}"},
             "lastActivityOn":"{{Now.AddSeconds(5):O}}"}
            """;

        var game = GameDocument.Deserialize(json, 1).ToGame();

        Assert.Equal(id, game.Id);
        Assert.Equal("K7Q2M9XA", game.GameCode);
        Assert.Equal(GameState.Started, game.GameState);
        Assert.Equal(Now.AddSeconds(5), game.LastActivityOn);
        Assert.Equal(playerOne, game.PlayerOne.PlayerId);
        Assert.True(game.PlayerOne.HasTurn);
        Assert.False(game.PlayerOne.IsReady);
        Assert.Empty(game.PlayerOne.Ships);
        Assert.Equal([new Shot(3, 4, false), new Shot(5, 6, false)], game.PlayerOne.Shots);
        Assert.Equal(Now, game.PlayerOne.LastActivityOn);
        Assert.Equal(playerTwo, game.PlayerTwo!.PlayerId);
        Assert.False(game.PlayerTwo.IsReady);
    }

    [Fact]
    public void VersionOne_WithoutASecondPlayerOrCoordinatesIsUpgraded()
    {
        var json = $$"""
            {"id":"{{Guid.NewGuid()}}","gameCode":"K7Q2M9XA","gameState":"New",
             "playerOne":{"playerId":"{{Guid.NewGuid()}}","playerName":"Admiral Nelson","hasTurn":false,
               "lastActivityOn":"{{Now:O}}"},
             "playerTwo":null,"lastActivityOn":"{{Now:O}}"}
            """;

        var document = GameDocumentV1.Deserialize(json).Upgrade();

        Assert.Null(document.PlayerTwo);
        Assert.Empty(document.PlayerOne.Ships);
        Assert.Empty(document.PlayerOne.Shots);
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
