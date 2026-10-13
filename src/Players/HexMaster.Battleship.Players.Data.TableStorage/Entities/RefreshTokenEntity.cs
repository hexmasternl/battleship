using Azure;
using Azure.Data.Tables;

namespace HexMaster.Battleship.Players.Data.TableStorage.Entities;

// A row of the refreshtokens table, keyed by the Base64Url SHA-256 hash of the token value. The
// value itself is never stored.
public sealed class RefreshTokenEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    // Null until the token is exchanged for a new pair.
    public DateTimeOffset? ConsumedAt { get; set; }
}
