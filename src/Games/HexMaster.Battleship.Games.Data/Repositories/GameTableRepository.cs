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
