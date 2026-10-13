using Azure;
using Azure.Data.Tables;

namespace HexMaster.Battleship.Players.Data.Entities;

// A row of the users table: partitioned by the first two characters of the user id, keyed by the id.
public sealed class UserEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public bool IsRevoked { get; set; }
}
