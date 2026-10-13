using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Data.TableStorage.Repositories;
using HexMaster.Battleship.Players.DomainModels;
using HexMaster.Battleship.Players.Services;
using HexMaster.Battleship.Players.Tests.Factories;

namespace HexMaster.Battleship.Players.Tests.Storage;

// Runs against Azurite; every test uses fresh ids, so the shared tables need no cleanup.
public class TableRepositoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    [AzuriteFact]
    public async Task User_RoundTripsAndComesBackPristine()
    {
        var repository = new UserTableRepository(Azurite.CreateClient());
        var user = User.Create(UserFactory.DisplayName(), Now);

        await repository.SaveAsync(user, CancellationToken.None);
        var stored = await repository.GetAsync(user.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(DomainModelState.Pristine, stored.State);
        Assert.Equal(user.DisplayName, stored.DisplayName);
        Assert.Equal(Now, stored.CreatedAt);
        Assert.Equal(Now, stored.LastSeenAt);
        Assert.False(stored.IsRevoked);
    }

    [AzuriteFact]
    public async Task User_ModifiedIsUpdated()
    {
        var repository = new UserTableRepository(Azurite.CreateClient());
        var user = User.Create(UserFactory.DisplayName(), Now);
        await repository.SaveAsync(user, CancellationToken.None);

        var stored = await repository.GetAsync(user.Id, CancellationToken.None);
        stored!.Touch(Now.AddHours(1));
        stored.Revoke();
        await repository.SaveAsync(stored, CancellationToken.None);

        var reread = await repository.GetAsync(user.Id, CancellationToken.None);
        Assert.Equal(Now.AddHours(1), reread!.LastSeenAt);
        Assert.True(reread.IsRevoked);
    }

    [AzuriteFact]
    public async Task User_UnknownIdReturnsNull()
    {
        var repository = new UserTableRepository(Azurite.CreateClient());

        Assert.Null(await repository.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [AzuriteFact]
    public async Task RefreshToken_RoundTripsAndComesBackPristine()
    {
        var repository = new RefreshTokenTableRepository(Azurite.CreateClient());
        var hash = RefreshTokenGenerator.Hash(new RefreshTokenGenerator().Generate());
        var token = RefreshToken.Create(hash, Guid.NewGuid(), Now, TimeSpan.FromDays(90));

        await repository.SaveAsync(token, CancellationToken.None);
        var stored = await repository.GetAsync(hash, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(DomainModelState.Pristine, stored.State);
        Assert.Equal(token.UserId, stored.UserId);
        Assert.Equal(Now, stored.IssuedAt);
        Assert.Equal(Now.AddDays(90), stored.ExpiresAt);
        Assert.Null(stored.ConsumedAt);
    }

    [AzuriteFact]
    public async Task RefreshToken_SecondConsumeOfTheSameTokenFails()
    {
        var repository = new RefreshTokenTableRepository(Azurite.CreateClient());
        var hash = RefreshTokenGenerator.Hash(new RefreshTokenGenerator().Generate());
        await repository.SaveAsync(RefreshToken.Create(hash, Guid.NewGuid(), Now, TimeSpan.FromDays(90)), CancellationToken.None);

        // Two requests read the same unconsumed token before either writes.
        var first = await repository.GetAsync(hash, CancellationToken.None);
        var second = await repository.GetAsync(hash, CancellationToken.None);
        first!.Consume(Now.AddMinutes(1));
        second!.Consume(Now.AddMinutes(1));

        await repository.SaveAsync(first, CancellationToken.None);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.SaveAsync(second, CancellationToken.None));

        var stored = await repository.GetAsync(hash, CancellationToken.None);
        Assert.Equal(Now.AddMinutes(1), stored!.ConsumedAt);
    }

    [AzuriteFact]
    public async Task RefreshToken_UnknownHashReturnsNull()
    {
        var repository = new RefreshTokenTableRepository(Azurite.CreateClient());

        Assert.Null(await repository.GetAsync(RefreshTokenFactory.Hash(), CancellationToken.None));
    }
}
