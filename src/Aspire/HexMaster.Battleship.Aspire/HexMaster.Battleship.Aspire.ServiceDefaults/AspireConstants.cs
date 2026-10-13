namespace HexMaster.Battleship.Aspire;

// Names shared by the AppHost, the integration event sender, the Events subscription and the
// API reference every API serves.
// Compiled into each of those projects as a linked file. Internal, so every assembly gets its
// own private copy and a project that references several of them sees no conflicting type.
internal static class AspireConstants
{
    public const string PubSubName = "pubsub";

    public const string IntegrationEventsTopic = "integration-events";

    // Where each API serves its Scalar API reference in Development; the AppHost links to it.
    public const string ApiReferencePath = "/scalar";
}
