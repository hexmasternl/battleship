namespace HexMaster.Battleship.Events.Replay;

// Bound from Events:Replay. Covers a short reconnect gap, not a durable history.
public sealed class ReplayOptions
{
    public const string ConfigurationSection = "Events:Replay";

    public int Capacity { get; set; } = 1000;

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(2);
}
