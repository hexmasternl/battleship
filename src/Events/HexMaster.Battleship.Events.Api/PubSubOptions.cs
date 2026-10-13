using HexMaster.Battleship.Aspire;

namespace HexMaster.Battleship.Events.Api;

// Bound from Events:PubSub. The component and topic names are the only contract with the
// publishing services; the defaults are the names the AppHost declares and the sender uses.
public sealed class PubSubOptions
{
    public const string ConfigurationSection = "Events:PubSub";

    public string Name { get; set; } = AspireConstants.PubSubName;

    public string Topic { get; set; } = AspireConstants.IntegrationEventsTopic;
}
