using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Data.Entities;

namespace HexMaster.Battleship.Games.Data.Repositories;

public sealed class GameTableRepository(TableServiceClient service)
    : TableRepository(service, TableName), IGameRepository
{
    public const string TableName = "games";

    public async Task<bool> TryAddAsync(IGame game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);

        var table = await GetTableAsync(cancellationToken);
        try
        {
            // Insert only: an existing row with the same code is the uniqueness check, so there is
            // no read-before-write race between two games drawing the same code.
            await table.AddEntityAsync(ToEntity(game), cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            return false;
        }
    }

    public async Task<IGame?> GetByCodeAsync(string gameCode, CancellationToken cancellationToken) =>
        (await GetForUpdateAsync(gameCode, cancellationToken))?.Game;

    public async Task<GameForUpdate?> GetForUpdateAsync(string gameCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameCode);

        var table = await GetTableAsync(cancellationToken);
        var response = await table.GetEntityIfExistsAsync<GameEntity>(
            gameCode, GameEntity.GameRowKey, cancellationToken: cancellationToken);
        if (!response.HasValue || response.Value is not { } entity)
        {
            return null;
        }

        var game = GameDocument.Deserialize(entity.Document, entity.DocumentVersion).ToGame();
        return new GameForUpdate(game, entity.ETag.ToString());
    }

    public async Task<bool> TryUpdateAsync(IGame game, string concurrencyToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(concurrencyToken);

        var table = await GetTableAsync(cancellationToken);
        try
        {
            // Replace, not merge: the game is one JSON document, so a write is always the whole game.
            await table.UpdateEntityAsync(
                ToEntity(game), new ETag(concurrencyToken), TableUpdateMode.Replace, cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status == 412)
        {
            return false;
        }
    }

    public async IAsyncEnumerable<InactiveGame> GetInactiveAsync(
        DateTimeOffset cutoff, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var table = await GetTableAsync(cancellationToken);

        // Filtered server-side on the activity column and selecting the keys only: the ETag comes
        // back with every row, so the document is never transferred, let alone deserialized.
        var filter = TableClient.CreateQueryFilter(
            $"RowKey eq {GameEntity.GameRowKey} and LastActivityOn lt {cutoff.ToUniversalTime()}");
        var rows = table.QueryAsync<TableEntity>(
            filter, select: [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)], cancellationToken: cancellationToken);
        await foreach (var row in rows)
        {
            yield return new InactiveGame(row.PartitionKey, row.ETag.ToString());
        }
    }

    public async Task<DeleteOutcome> TryDeleteAsync(string gameCode, string concurrencyToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(concurrencyToken);

        var table = await GetTableAsync(cancellationToken);
        try
        {
            // The SDK answers a delete of a missing row with its 404 response instead of throwing.
            var response = await table.DeleteEntityAsync(
                gameCode, GameEntity.GameRowKey, new ETag(concurrencyToken), cancellationToken);
            return response.Status == 404 ? DeleteOutcome.NotFound : DeleteOutcome.Deleted;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return DeleteOutcome.NotFound;
        }
        catch (RequestFailedException exception) when (exception.Status == 412)
        {
            return DeleteOutcome.Changed;
        }
    }

    private static GameEntity ToEntity(IGame game) => new()
    {
        PartitionKey = game.GameCode,
        RowKey = GameEntity.GameRowKey,
        GameId = game.Id,
        State = game.GameState.ToString(),
        LastActivityOn = game.LastActivityOn,
        Document = GameDocument.From(game).Serialize(),
        DocumentVersion = GameDocument.CurrentVersion,
    };
}
