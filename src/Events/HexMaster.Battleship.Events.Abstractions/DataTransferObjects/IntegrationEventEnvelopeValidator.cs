namespace HexMaster.Battleship.Events.Abstractions.DataTransferObjects;

// Checks the rules every envelope must satisfy before it is routed. Returns the errors keyed
// by property name (the shape of a validation problem response); an empty result means valid.
public static class IntegrationEventEnvelopeValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(IntegrationEventEnvelope? envelope)
    {
        var errors = new Dictionary<string, string[]>();

        if (envelope is null)
        {
            errors["envelope"] = ["The request body must be an integration event envelope."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(envelope.Id))
        {
            errors[nameof(envelope.Id)] = ["An event id is required."];
        }

        if (string.IsNullOrWhiteSpace(envelope.Type))
        {
            errors[nameof(envelope.Type)] = ["An event type is required."];
        }

        if (!Enum.IsDefined(envelope.Target))
        {
            errors[nameof(envelope.Target)] = ["The target kind must be 'user', 'game' or 'all'."];
        }
        else if (envelope.Target is EventTarget.User or EventTarget.Game
                 && (envelope.TargetIds is null || !envelope.TargetIds.Any(id => !string.IsNullOrWhiteSpace(id))))
        {
            errors[nameof(envelope.TargetIds)] = ["A user or game event must list at least one target user id."];
        }

        return errors;
    }
}
