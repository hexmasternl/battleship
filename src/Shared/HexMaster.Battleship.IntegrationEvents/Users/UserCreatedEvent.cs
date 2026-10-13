namespace HexMaster.Battleship.IntegrationEvents.Users;

public sealed record UserCreatedEvent(Guid UserId, string DisplayName) : IIntegrationEvent
{
    public static string EventType => "user.created.v1";
}
