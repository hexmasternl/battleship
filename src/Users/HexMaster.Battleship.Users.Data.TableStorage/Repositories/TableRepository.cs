using Azure.Data.Tables;

namespace HexMaster.Battleship.Users.Data.TableStorage.Repositories;

// Hands out the repository's table, creating it on first use so an empty storage account needs no
// manual provisioning. Concurrent first calls may both create it; CreateIfNotExists is idempotent.
public abstract class TableRepository(TableServiceClient service, string tableName)
{
    private readonly TableClient _table = service.GetTableClient(tableName);
    private volatile bool _created;

    protected async Task<TableClient> GetTableAsync(CancellationToken cancellationToken)
    {
        if (!_created)
        {
            await _table.CreateIfNotExistsAsync(cancellationToken);
            _created = true;
        }

        return _table;
    }
}
