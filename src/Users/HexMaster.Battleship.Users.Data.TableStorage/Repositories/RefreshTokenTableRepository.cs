using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Users.Abstractions.DomainModels;
using HexMaster.Battleship.Users.Abstractions.Repositories;
using HexMaster.Battleship.Users.Data.TableStorage.Entities;
using HexMaster.Battleship.Users.DomainModels;

namespace HexMaster.Battleship.Users.Data.TableStorage.Repositories;

public sealed class RefreshTokenTableRepository(TableServiceClient service)
    : TableRepository(service, TableName), IRefreshTokenRepository
{
    public const string TableName = "refreshtokens";

    // The ETag each token had when it was read. Domain models know nothing of storage, so the
    // repository remembers it beside the model for as long as the model is alive.
    private readonly ConditionalWeakTable<IRefreshToken, ReadETag> _etags = new();

    public async Task<IRefreshToken?> GetAsync(string hash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var table = await GetTableAsync(cancellationToken);
        var response = await table.GetEntityIfExistsAsync<RefreshTokenEntity>(
            PartitionKeyFor(hash), hash, cancellationToken: cancellationToken);
        if (!response.HasValue || response.Value is not { } entity)
        {
            return null;
        }

        var token = RefreshToken.Reconcile(entity.RowKey, entity.UserId, entity.IssuedAt, entity.ExpiresAt, entity.ConsumedAt);
        _etags.AddOrUpdate(token, new ReadETag(entity.ETag));
        return token;
    }

    public async Task SaveAsync(IRefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.State is DomainModelState.Pristine or DomainModelState.Touched)
        {
            return;
        }

        var table = await GetTableAsync(cancellationToken);
        var entity = ToEntity(token);
        try
        {
            switch (token.State)
            {
                case DomainModelState.New:
                    await table.AddEntityAsync(entity, cancellationToken);
                    break;
                case DomainModelState.Modified:
                    // Conditioned on the row being unchanged since it was read: of two requests that
                    // consume the same token, only the first update succeeds.
                    await table.UpdateEntityAsync(entity, ETagOf(token), TableUpdateMode.Replace, cancellationToken);
                    break;
                case DomainModelState.Deleted:
                    await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETagOf(token), cancellationToken);
                    break;
            }
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            throw new ConcurrencyConflictException("The refresh token changed in the store.", exception);
        }
    }

    // A fixed prefix plus the first two characters of the hash, so tokens spread over partitions.
    public static string PartitionKeyFor(string hash) => $"token{hash[..2]}";

    private ETag ETagOf(IRefreshToken token) =>
        _etags.TryGetValue(token, out var read)
            ? read.Value
            : throw new InvalidOperationException("Only a refresh token read from this repository can be updated or deleted.");

    private static RefreshTokenEntity ToEntity(IRefreshToken token) => new()
    {
        PartitionKey = PartitionKeyFor(token.Id),
        RowKey = token.Id,
        UserId = token.UserId,
        IssuedAt = token.IssuedAt,
        ExpiresAt = token.ExpiresAt,
        ConsumedAt = token.ConsumedAt,
    };

    private sealed record ReadETag(ETag Value);
}
