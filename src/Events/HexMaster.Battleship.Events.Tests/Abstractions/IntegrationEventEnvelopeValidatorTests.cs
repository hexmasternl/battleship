using System.Text.Json;
using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Events.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Events.Tests.Factories;

namespace HexMaster.Battleship.Events.Tests.Abstractions;

public class IntegrationEventEnvelopeValidatorTests
{
    [Fact]
    public void Validate_GameEnvelopeWithTwoTargets_IsAccepted()
    {
        var envelope = IntegrationEventEnvelopeFactory.ForGame("user-a", "user-b");

        Assert.Empty(IntegrationEventEnvelopeValidator.Validate(envelope));
    }

    [Fact]
    public void Validate_AllEnvelopeWithoutTargets_IsAccepted()
    {
        Assert.Empty(IntegrationEventEnvelopeValidator.Validate(IntegrationEventEnvelopeFactory.ForAll()));
    }

    [Fact]
    public void Validate_NullEnvelope_IsRejected()
    {
        Assert.NotEmpty(IntegrationEventEnvelopeValidator.Validate(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Validate_MissingId_IsRejected(string? id)
    {
        var envelope = IntegrationEventEnvelopeFactory.Create() with { Id = id! };

        Assert.Contains(nameof(IntegrationEventEnvelope.Id), IntegrationEventEnvelopeValidator.Validate(envelope).Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_MissingType_IsRejected(string? type)
    {
        var envelope = IntegrationEventEnvelopeFactory.Create() with { Type = type! };

        Assert.Contains(nameof(IntegrationEventEnvelope.Type), IntegrationEventEnvelopeValidator.Validate(envelope).Keys);
    }

    [Fact]
    public void Validate_MissingTargetKind_IsRejected()
    {
        var envelope = IntegrationEventEnvelopeFactory.Create() with { Target = default };

        Assert.Contains(nameof(IntegrationEventEnvelope.Target), IntegrationEventEnvelopeValidator.Validate(envelope).Keys);
    }

    [Theory]
    [InlineData(EventTarget.User)]
    [InlineData(EventTarget.Game)]
    public void Validate_UserOrGameWithoutTargets_IsRejected(EventTarget target)
    {
        var envelope = IntegrationEventEnvelopeFactory.Create(target) with { TargetIds = [] };

        Assert.Contains(nameof(IntegrationEventEnvelope.TargetIds), IntegrationEventEnvelopeValidator.Validate(envelope).Keys);
    }

    [Fact]
    public void Validate_UserWithOnlyBlankTargets_IsRejected()
    {
        var envelope = IntegrationEventEnvelopeFactory.Create(EventTarget.User) with { TargetIds = [" "] };

        Assert.Contains(nameof(IntegrationEventEnvelope.TargetIds), IntegrationEventEnvelopeValidator.Validate(envelope).Keys);
    }

    [Fact]
    public void Deserialize_LowercaseTargetKind_BindsToEnum()
    {
        const string json = """
            {"id":"1","type":"shot-fired","target":"game","targetIds":["a","b"],"occurredAt":"2026-10-13T10:00:00Z","payload":{"x":1}}
            """;

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(json, JsonSerializerOptions.Web);

        Assert.NotNull(envelope);
        Assert.Equal(EventTarget.Game, envelope.Target);
        Assert.Equal(["a", "b"], envelope.TargetIds);
        Assert.Equal(1, envelope.Payload.GetProperty("x").GetInt32());
    }

    [Fact]
    public void Serialize_TargetKind_IsWrittenLowercase()
    {
        var json = JsonSerializer.Serialize(IntegrationEventEnvelopeFactory.ForAll(), JsonSerializerOptions.Web);

        Assert.Contains("\"target\":\"all\"", json);
    }

    [Fact]
    public void Deserialize_UnknownTargetKind_Throws()
    {
        const string json = """{"id":"1","type":"t","target":"team","targetIds":[],"payload":{}}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IntegrationEventEnvelope>(json, JsonSerializerOptions.Web));
    }

    [Fact]
    public void Deserialize_PreRenamePlayerTargetKind_Throws()
    {
        const string json = """{"id":"1","type":"t","target":"player","targetIds":["a"],"payload":{}}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IntegrationEventEnvelope>(json, JsonSerializerOptions.Web));
    }

    [Fact]
    public void Deserialize_UserTargetKind_BindsToUser()
    {
        const string json = """{"id":"1","type":"t","target":"user","targetIds":["a"],"payload":{}}""";

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(json, JsonSerializerOptions.Web);

        Assert.Equal(EventTarget.User, envelope!.Target);
    }
}
