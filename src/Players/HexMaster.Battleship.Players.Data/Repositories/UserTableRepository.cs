using Azure;
using Azure.Data.Tables;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DomainModels;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Data.Entities;
using HexMaster.Battleship.Players.DomainModels;

namespace HexMaster.Battleship.Players.Data.Repositories;

public sealed class UserTableRepository(TableServiceClient service)
    : TableRepository(service, TableName), IUserRepository
{
    public const string TableName = "users";

    public async Task<IUser?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var table = await GetTableAsync(cancellationToken);
        var response = await table.GetEntityIfExistsAsync<UserEntity>(
            PartitionKeyFor(id), id.ToString(), cancellationToken: cancellationToken);
        if (!response.HasValue || response.Value is not { } entity)
        {
            return null;
        }

        return User.Reconcile(Guid.Parse(entity.RowKey), entity.DisplayName, entity.CreatedAt, entity.LastSeenAt, entity.IsRevoked);
    }

    public async Task SaveAsync(IUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.State is DomainModelState.Pristine or DomainModelState.Touched)
        {
            return;
        }

        var table = await GetTableAsync(cancellationToken);
        var entity = ToEntity(user);
        try
        {
            switch (user.State)
            {
                case DomainModelState.New:
                    await table.AddEntityAsync(entity, cancellationToken);
                    break;
                case DomainModelState.Modified:
                    // Last writer wins: two browsers of one user may touch the row at the same moment,
                    // and both last-seen times are equally right.
                    await table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace, cancellationToken);
                    break;
                case DomainModelState.Deleted:
                    await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken);
                    break;
            }
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            throw new ConcurrencyConflictException($"User {user.Id} changed in the store.", exception);
        }
    }

    // Spreads users over partitions; the id is a Guid, so its first two characters are well mixed.
    public static string PartitionKeyFor(Guid id) => id.ToString()[..2];

    private static UserEntity ToEntity(IUser user) => new()
    {
        PartitionKey = PartitionKeyFor(user.Id),
        RowKey = user.Id.ToString(),
        DisplayName = user.DisplayName,
        CreatedAt = user.CreatedAt,
        LastSeenAt = user.LastSeenAt,
        IsRevoked = user.IsRevoked,
    };
}
