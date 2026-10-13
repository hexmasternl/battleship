using Azure.Data.Tables;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Data.Entities;
using HexMaster.Battleship.Games.Data.Repositories;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Tests.Factories;

namespace HexMaster.Battleship.Games.Tests.Storage;

// Runs against Azurite; every test uses a fresh game code, so the shared table needs no cleanup.
public class GameTableRepositoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    [AzuriteFact]
    public async Task NewGame_RoundTripsAndComesBackPristine()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);

        Assert.True(await repository.TryAddAsync(game, CancellationToken.None));
        var stored = await repository.GetByCodeAsync(game.GameCode, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(DomainModelState.Pristine, stored.State);
        Assert.Equal(game.Id, stored.Id);
        Assert.Equal(game.GameCode, stored.GameCode);
        Assert.Equal(GameState.New, stored.GameState);
        Assert.Equal(Now, stored.LastActivityOn);
        Assert.Equal(game.PlayerOne.PlayerId, stored.PlayerOne.PlayerId);
        Assert.Equal(game.PlayerOne.PlayerName, stored.PlayerOne.PlayerName);
        Assert.False(stored.PlayerOne.HasTurn);
        Assert.False(stored.PlayerOne.IsReady);
        Assert.Empty(stored.PlayerOne.Ships);
        Assert.Empty(stored.PlayerOne.Shots);
        Assert.Equal(Now, stored.PlayerOne.LastActivityOn);
        Assert.Null(stored.PlayerTwo);
    }

    [AzuriteFact]
    public async Task StartedGame_RoundTripsPlayersShipsAndShots()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());
        var playerOne = GameFactory.Player(
            hasTurn: true, isReady: true, ships: GameFactory.FullFleet(), shots: [new(4, 7, true)], lastActivityOn: Now);
        var playerTwo = GameFactory.Player(
            isReady: true, ships: GameFactory.FullFleet(), shots: [new(9, 9, false)], lastActivityOn: Now.AddMinutes(1));
        var game = Game.Reconcile(Guid.NewGuid(), GameFactory.Code(), GameState.Started, playerOne, playerTwo, Now.AddMinutes(1));

        Assert.True(await repository.TryAddAsync(game, CancellationToken.None));
        var stored = await repository.GetByCodeAsync(game.GameCode, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(GameState.Started, stored.GameState);
        Assert.True(stored.PlayerOne.HasTurn);
        Assert.True(stored.PlayerOne.IsReady);
        Assert.Equal(playerOne.Ships, stored.PlayerOne.Ships);
        Assert.Equal(playerOne.Shots, stored.PlayerOne.Shots);
        Assert.Equal(playerTwo.PlayerId, stored.PlayerTwo!.PlayerId);
        Assert.Equal(playerTwo.Ships, stored.PlayerTwo.Ships);
        Assert.Equal(playerTwo.Shots, stored.PlayerTwo.Shots);
        Assert.Equal(Now.AddMinutes(1), stored.PlayerTwo.LastActivityOn);
    }

    [AzuriteFact]
    public async Task TryAdd_ReturnsFalseForACodeInUseAndKeepsTheStoredGame()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());
        var code = GameFactory.Code();
        var first = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), code, Now);
        var second = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), code, Now);

        Assert.True(await repository.TryAddAsync(first, CancellationToken.None));
        Assert.False(await repository.TryAddAsync(second, CancellationToken.None));

        var stored = await repository.GetByCodeAsync(code.Value, CancellationToken.None);
        Assert.Equal(first.Id, stored!.Id);
    }

    [AzuriteFact]
    public async Task Row_HoldsTheColumnsAndTheVersionedDocument()
    {
        var client = Azurite.CreateClient();
        var repository = new GameTableRepository(client);
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);
        await repository.TryAddAsync(game, CancellationToken.None);

        var row = (await client.GetTableClient(GameTableRepository.TableName)
            .GetEntityAsync<TableEntity>(game.GameCode, GameEntity.GameRowKey)).Value;

        Assert.Equal(game.Id, row.GetGuid(nameof(GameEntity.GameId)));
        Assert.Equal("New", row.GetString(nameof(GameEntity.State)));
        Assert.Equal(Now, row.GetDateTimeOffset(nameof(GameEntity.LastActivityOn)));
        Assert.Equal(GameDocument.CurrentVersion, row.GetInt32(nameof(GameEntity.DocumentVersion)));
        Assert.Contains($"\"gameCode\":\"{game.GameCode}\"", row.GetString(nameof(GameEntity.Document)));
    }

    [AzuriteFact]
    public async Task TryUpdate_WithAFreshTokenStoresTheGame()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);
        await repository.TryAddAsync(game, CancellationToken.None);
        var read = await repository.GetForUpdateAsync(game.GameCode, CancellationToken.None);
        var playerTwoId = Guid.NewGuid();
        ((Game)read!.Game).Join(playerTwoId, "Admiral Tromp", Now.AddMinutes(1));

        Assert.False(string.IsNullOrEmpty(read.ConcurrencyToken));
        Assert.True(await repository.TryUpdateAsync(read.Game, read.ConcurrencyToken, CancellationToken.None));

        var stored = await repository.GetForUpdateAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(playerTwoId, stored!.Game.PlayerTwo!.PlayerId);
        Assert.Equal(Now.AddMinutes(1), stored.Game.LastActivityOn);
        Assert.NotEqual(read.ConcurrencyToken, stored.ConcurrencyToken);
    }

    [AzuriteFact]
    public async Task TryUpdate_WithAStaleTokenReturnsFalseAndKeepsTheStoredGame()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());
        var game = Game.Create(Guid.NewGuid(), GameFactory.PlayerName(), GameFactory.Code(), Now);
        await repository.TryAddAsync(game, CancellationToken.None);
        var first = await repository.GetForUpdateAsync(game.GameCode, CancellationToken.None);
        var second = await repository.GetForUpdateAsync(game.GameCode, CancellationToken.None);
        var winner = Guid.NewGuid();
        ((Game)first!.Game).Join(winner, GameFactory.PlayerName(), Now.AddMinutes(1));
        ((Game)second!.Game).Join(Guid.NewGuid(), GameFactory.PlayerName(), Now.AddMinutes(2));

        Assert.True(await repository.TryUpdateAsync(first.Game, first.ConcurrencyToken, CancellationToken.None));
        Assert.False(await repository.TryUpdateAsync(second.Game, second.ConcurrencyToken, CancellationToken.None));

        var stored = await repository.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(winner, stored!.PlayerTwo!.PlayerId);
    }

    [AzuriteFact]
    public async Task GetForUpdate_UnknownCodeReturnsNull()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());

        Assert.Null(await repository.GetForUpdateAsync(GameFactory.Code().Value, CancellationToken.None));
    }

    [AzuriteFact]
    public async Task GetByCode_UnknownCodeReturnsNull()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());

        Assert.Null(await repository.GetByCodeAsync(GameFactory.Code().Value, CancellationToken.None));
    }
}
