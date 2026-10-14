namespace HexMaster.Battleship.Aspire.Tests;

/// <summary>
/// Tests that build the whole AppHost model. Each build writes the Dapr tracing configuration to the
/// same temp file, so they run one after another instead of in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppHostModelCollection
{
    public const string Name = "AppHost model";
}
