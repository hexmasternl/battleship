namespace HexMaster.Battleship.Events.Api;

// Bound from Events:PubSub. The component and topic names are the only contract with the
// publishing services.
public sealed class PubSubOptions
{
    public const string ConfigurationSection = "Events:PubSub";

    public string Name { get; set; } = "pubsub";

    public string Topic { get; set; } = "integration-events";
}
