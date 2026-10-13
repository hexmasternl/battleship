using System.Text.Json;

namespace HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

// The one shape every service publishes on the integration-events topic. The Events service
// routes on Target and TargetIds only and never inspects Payload. A game event lists its
// participants in TargetIds, so routing needs no game membership state.
public sealed record IntegrationEventEnvelope(
    string Id,
    string Type,
    EventTarget Target,
    IReadOnlyList<string> TargetIds,
    DateTimeOffset OccurredAt,
    JsonElement Payload);
