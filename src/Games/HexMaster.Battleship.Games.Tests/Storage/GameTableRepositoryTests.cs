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
        Assert.Empty(stored.PlayerOne.ShipCoordinates);
        Assert.Empty(stored.PlayerOne.ShotCoordinates);
        Assert.Equal(Now, stored.PlayerOne.LastActivityOn);
        Assert.Null(stored.PlayerTwo);
    }

    [AzuriteFact]
    public async Task StartedGame_RoundTripsPlayersAndCoordinates()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());
        var playerOne = GameFactory.Player(hasTurn: true, ships: [new(0, 0), new(0, 1)], shots: [new(4, 7)], lastActivityOn: Now);
        var playerTwo = GameFactory.Player(ships: [new(9, 9)], lastActivityOn: Now.AddMinutes(1));
        var game = Game.Reconcile(Guid.NewGuid(), GameFactory.Code(), GameState.Started, playerOne, playerTwo, Now.AddMinutes(1));

        Assert.True(await repository.TryAddAsync(game, CancellationToken.None));
        var stored = await repository.GetByCodeAsync(game.GameCode, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(GameState.Started, stored.GameState);
        Assert.True(stored.PlayerOne.HasTurn);
        Assert.Equal(playerOne.ShipCoordinates, stored.PlayerOne.ShipCoordinates);
        Assert.Equal(playerOne.ShotCoordinates, stored.PlayerOne.ShotCoordinates);
        Assert.Equal(playerTwo.PlayerId, stored.PlayerTwo!.PlayerId);
        Assert.Equal(playerTwo.ShipCoordinates, stored.PlayerTwo.ShipCoordinates);
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
    public async Task GetByCode_UnknownCodeReturnsNull()
    {
        var repository = new GameTableRepository(Azurite.CreateClient());

        Assert.Null(await repository.GetByCodeAsync(GameFactory.Code().Value, CancellationToken.None));
    }
}
