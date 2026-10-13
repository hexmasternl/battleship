using Azure.Data.Tables;

namespace HexMaster.Battleship.Games.Tests.Storage;

// The table storage the storage tests run against. Defaults to a local Azurite on its standard
// ports; set BATTLESHIP_TESTS_TABLES to another connection string (for example the emulator an
// `aspire run` started) to use that instead.
public static class Azurite
{
    public const string ConnectionStringVariable = "BATTLESHIP_TESTS_TABLES";

    private static readonly Lazy<string?> UnreachableReason = new(Probe);

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } configured
            ? configured
            : "UseDevelopmentStorage=true";

    // Null when the storage answers; otherwise why the storage tests are skipped.
    public static string? SkipReason => UnreachableReason.Value;

    public static TableServiceClient CreateClient() => new(ConnectionString);

    private static string? Probe()
    {
        try
        {
            var options = new TableClientOptions { Retry = { MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(3) } };
            var client = new TableServiceClient(ConnectionString, options);
            client.GetProperties();
            return null;
        }
        catch (Exception exception)
        {
            return $"Table storage is not reachable ({exception.GetType().Name}). Start Azurite (docker run -p 10002:10002 " +
                $"mcr.microsoft.com/azure-storage/azurite azurite-table --tableHost 0.0.0.0) or set {ConnectionStringVariable}.";
        }
    }
}

// A fact that runs only when table storage is reachable, and is skipped with the reason otherwise.
public sealed class AzuriteFactAttribute : FactAttribute
{
    public AzuriteFactAttribute()
    {
        if (Azurite.SkipReason is { } reason)
        {
            Skip = reason;
        }
    }
}
