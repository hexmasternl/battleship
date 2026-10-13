namespace HexMaster.Battleship.IntegrationEvents.Users;

// The user's display name changed.
public sealed record UserUpdatedEvent(Guid UserId, string DisplayName) : IIntegrationEvent
{
    public static string EventType => "user.updated.v1";
}
