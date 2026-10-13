namespace HexMaster.Battleship.Aspire;

// Names shared by the AppHost, the integration event sender and the Events subscription.
// Compiled into each of those projects as a linked file. Internal, so every assembly gets its
// own private copy and a project that references several of them sees no conflicting type.
internal static class AspireConstants
{
    public const string PubSubName = "pubsub";

    public const string IntegrationEventsTopic = "integration-events";
}
