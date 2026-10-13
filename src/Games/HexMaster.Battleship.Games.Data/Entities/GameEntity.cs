using Azure;
using Azure.Data.Tables;

namespace HexMaster.Battleship.Games.Data.Entities;

// A row of the games table: partitioned and keyed by the game code, so the code is unique and every
// lookup by code is a point read. The full game is the JSON Document; id, state and activity are
// repeated as columns so the table can be inspected and filtered without parsing it.
public sealed class GameEntity : ITableEntity
{
    public const string GameRowKey = "game";

    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = GameRowKey;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid GameId { get; set; }

    public string State { get; set; } = string.Empty;

    public DateTimeOffset LastActivityOn { get; set; }

    public string Document { get; set; } = string.Empty;

    public int DocumentVersion { get; set; }
}
