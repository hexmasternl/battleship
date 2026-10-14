using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Data.Entities;

namespace HexMaster.Battleship.Games.Tests.Fakes;

// Stores games as their serialized documents with a version token, the way table storage keeps a
// row with an ETag: every read hands out an independent copy, and an update or delete with a stale
// token is refused. The last activity is kept beside the document, like the table's column, so
// selecting inactive games never reads a document. Lets concurrency tests race real handlers
// without Azurite.
public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<string, Row> _rows = new();
    private int _version;

    // Runs after a read and before the caller gets the game; lets a test interleave two requests.
    public Func<Task>? AfterRead { get; set; }

    // Runs after an inactive game is selected and before it is handed out; lets a test change the
    // game between the cleanup's selection and its delete.
    public Func<InactiveGame, Task>? AfterSelect { get; set; }

    public Task<bool> TryAddAsync(IGame game, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.TryAdd(game.GameCode, ToRow(game)));

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

        return Task.FromResult(_rows.TryUpdate(game.GameCode, ToRow(game), current));
    }

    public async IAsyncEnumerable<InactiveGame> GetInactiveAsync(
        DateTimeOffset cutoff, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var selected = _rows
            .Where(r => r.Value.LastActivityOn < cutoff)
            .Select(r => new InactiveGame(r.Key, r.Value.Token))
            .ToList();
        foreach (var inactive in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AfterSelect is { } afterSelect)
            {
                await afterSelect(inactive);
            }

            yield return inactive;
        }
    }

    public Task<DeleteOutcome> TryDeleteAsync(string gameCode, string concurrencyToken, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(gameCode, out var current))
        {
            return Task.FromResult(DeleteOutcome.NotFound);
        }

        if (current.Token != concurrencyToken)
        {
            return Task.FromResult(DeleteOutcome.Changed);
        }

        return Task.FromResult(
            _rows.TryRemove(new KeyValuePair<string, Row>(gameCode, current)) ? DeleteOutcome.Deleted
            : _rows.ContainsKey(gameCode) ? DeleteOutcome.Changed
            : DeleteOutcome.NotFound);
    }

    // Stores a row whose document cannot be read back, as a corrupt or foreign row in the table would be.
    public void AddUnreadable(string gameCode, DateTimeOffset lastActivityOn) =>
        _rows[gameCode] = new Row("{not a game", lastActivityOn, NextToken());

    public bool Contains(string gameCode) => _rows.ContainsKey(gameCode);

    // The token of the stored version, as a read would return it.
    public string TokenOf(string gameCode) => _rows[gameCode].Token;

    private Row ToRow(IGame game) => new(GameDocument.From(game).Serialize(), game.LastActivityOn, NextToken());

    private string NextToken() => $"W/\"{Interlocked.Increment(ref _version)}\"";

    private sealed record Row(string Document, DateTimeOffset LastActivityOn, string Token);
}
