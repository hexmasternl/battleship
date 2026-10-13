using System.Text.Json;
using HexMaster.Battleship.IntegrationEvents.Games;
using HexMaster.Battleship.IntegrationEvents.Tests.Factories;

namespace HexMaster.Battleship.IntegrationEvents.Tests.Catalog;

// The payload contract clients depend on: camelCase property names, nothing more than the spec lists.
public class IntegrationEventSerializationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static TheoryData<object, string[]> PayloadShapes() => new()
    {
        { IntegrationEventFactory.PlayerCreated(), ["playerId", "playerName"] },
        { IntegrationEventFactory.PlayerUpdated(), ["playerId", "playerName"] },
        { IntegrationEventFactory.GameCreated(), ["gameId", "gameCode", "playerId"] },
        { IntegrationEventFactory.GameJoined(), ["gameId", "gameCode", "playerId", "playerName"] },
        { IntegrationEventFactory.GameStarted(), ["gameId", "gameCode", "playerWithTurnId"] },
        { IntegrationEventFactory.ShotFired(), ["gameId", "gameCode", "playerId", "column", "row"] },
        { IntegrationEventFactory.GameEnded(winnerId: Guid.NewGuid()), ["gameId", "gameCode", "reason", "winnerId"] }
    };

    [Theory]
    [MemberData(nameof(PayloadShapes))]
    public void Serialize_WritesExactlyTheCamelCasePayloadFields(object integrationEvent, string[] expected)
    {
        var payload = JsonSerializer.SerializeToElement(integrationEvent, integrationEvent.GetType(), Web);

        var names = payload.EnumerateObject().Select(p => p.Name);

        Assert.Equal(expected, names);
    }

    [Fact]
    public void Serialize_ShotFired_CarriesGameCodePlayerColumnAndRow()
    {
        var playerId = Guid.NewGuid();
        var shot = IntegrationEventFactory.ShotFired("ABCD1234", playerId, column: 3, row: 7);

        var payload = JsonSerializer.SerializeToElement(shot, Web);

        Assert.Equal(shot.GameId, payload.GetProperty("gameId").GetGuid());
        Assert.Equal("ABCD1234", payload.GetProperty("gameCode").GetString());
        Assert.Equal(playerId, payload.GetProperty("playerId").GetGuid());
        Assert.Equal(3, payload.GetProperty("column").GetInt32());
        Assert.Equal(7, payload.GetProperty("row").GetInt32());
    }

    [Fact]
    public void Serialize_GameEndedByServer_CarriesReasonAndNoWinner()
    {
        var ended = IntegrationEventFactory.GameEnded(GameEndReason.EndedByServer);

        var payload = JsonSerializer.SerializeToElement(ended, Web);

        Assert.Equal("ended-by-server", payload.GetProperty("reason").GetString());
        Assert.False(payload.TryGetProperty("winnerId", out _));
    }

    [Theory]
    [InlineData(GameEndReason.Completed, "completed")]
    [InlineData(GameEndReason.AbandonedByPlayer, "abandoned-by-player")]
    [InlineData(GameEndReason.EndedByServer, "ended-by-server")]
    public void Serialize_GameEndReason_IsKebabCase(GameEndReason reason, string expected)
    {
        // Default options: the converter on the enum must not depend on the publisher's settings.
        var json = JsonSerializer.Serialize(reason);

        Assert.Equal($"\"{expected}\"", json);
    }

    [Fact]
    public void Deserialize_GameEnded_RoundTrips()
    {
        var ended = IntegrationEventFactory.GameEnded(GameEndReason.AbandonedByPlayer, Guid.NewGuid());

        var roundTripped = JsonSerializer.Deserialize<GameEndedEvent>(JsonSerializer.Serialize(ended, Web), Web);

        Assert.Equal(ended, roundTripped);
    }
}
