namespace HexMaster.Battleship.Events.Streaming;

// Bound from Events:Stream.
public sealed class StreamOptions
{
    public const string ConfigurationSection = "Events:Stream";

    // Events queued per connection before the oldest is dropped.
    public int ChannelCapacity { get; set; } = 256;

    // Idle time after which a heartbeat is written; below common proxy idle timeouts.
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
}
