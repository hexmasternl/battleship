using HexMaster.Battleship.Events.Abstractions;

namespace HexMaster.Battleship.IntegrationEvents;

// Who an integration event is for. The publisher states it on every send, because the same
// event can go to different users depending on the context it is raised in. Maps onto the
// envelope's target kind and target user ids.
public readonly record struct EventAudience
{
    private EventAudience(EventTarget target, IReadOnlyList<string> targetIds)
    {
        Target = target;
        TargetIds = targetIds;
    }

    public EventTarget Target { get; }

    public IReadOnlyList<string> TargetIds => field ?? [];

    // Every connected user.
    public static EventAudience Everyone { get; } = new(EventTarget.All, []);

    // The listed users.
    public static EventAudience Users(params IEnumerable<Guid> userIds) =>
        new(EventTarget.Player, ToTargetIds(userIds));

    // The players of a game, by their user ids. Listing them keeps routing free of game
    // membership state.
    public static EventAudience Game(params IEnumerable<Guid> userIds) =>
        new(EventTarget.Game, ToTargetIds(userIds));

    private static string[] ToTargetIds(IEnumerable<Guid> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        return [.. userIds.Select(id => id.ToString())];
    }
}
