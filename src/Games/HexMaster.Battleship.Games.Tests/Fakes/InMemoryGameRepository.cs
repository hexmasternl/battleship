using System.Collections.Concurrent;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Data.Entities;

namespace HexMaster.Battleship.Games.Tests.Fakes;

// Stores games as their serialized documents with a version token, the way table storage keeps a
// row with an ETag: every read hands out an independent copy, and an update with a stale token is
// refused. Lets concurrency tests race real handlers without Azurite.
public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<string, (string Document, string Token)> _rows = new();
    private int _version;

    // Runs after a read and before the caller gets the game; lets a test interleave two requests.
    public Func<Task>? AfterRead { get; set; }

    public Task<bool> TryAddAsync(IGame game, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.TryAdd(game.GameCode, (GameDocument.From(game).Serialize(), NextToken())));

    public async Task<IGame?> GetByCodeAsync(string gameCode, CancellationToken cancellationToken) =>
        (await GetForUpdateAsync(gameCode, cancellationToken))?.Game;

    public async Task<GameForUpdate?> GetForUpdateAsync(string gameCode, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(gameCode, out var row))
        {
            return null;
        }

        if (AfterRead is { } afterRead)
        {
            await afterRead();
        }

        var game = GameDocument.Deserialize(row.Document, GameDocument.CurrentVersion).ToGame();
        return new GameForUpdate(game, row.Token);
    }

    public Task<bool> TryUpdateAsync(IGame game, string concurrencyToken, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(game.GameCode, out var current) || current.Token != concurrencyToken)
        {
            return Task.FromResult(false);
        }

        var updated = (GameDocument.From(game).Serialize(), NextToken());
        return Task.FromResult(_rows.TryUpdate(game.GameCode, updated, current));
    }

    // The token of the stored version, as a read would return it.
    public string TokenOf(string gameCode) => _rows[gameCode].Token;

    private string NextToken() => $"W/\"{Interlocked.Increment(ref _version)}\"";
}
