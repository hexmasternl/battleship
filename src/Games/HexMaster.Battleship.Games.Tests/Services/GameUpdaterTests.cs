using System.Diagnostics;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Services;

public class GameUpdaterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _games = new();
    private readonly Func<long> _conflicts;
    private readonly GameUpdater _updater;
    private readonly GameCode _code = GameFactory.Code();

    public GameUpdaterTests()
    {
        var telemetry = TelemetryFactory.Create(out var meterFactory);
        _conflicts = TelemetryFactory.Listen(meterFactory, GamesMetrics.UpdateConflictsName);
        _updater = new GameUpdater(_games.Object, telemetry, NullLogger<GameUpdater>.Instance);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForAnUnknownGame()
    {
        var mutated = false;

        var result = await _updater.UpdateAsync(_code, _ => { mutated = true; return GameMutation.Save(1); }, CancellationToken.None);

        Assert.False(result.Found);
        Assert.False(mutated);
        _games.Verify(g => g.TryUpdateAsync(It.IsAny<IGame>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WritesNothingForNoChange()
    {
        ReadReturns(("etag-1", NewGame()));

        var result = await _updater.UpdateAsync(_code, _ => GameMutation.NoChange("same"), CancellationToken.None);

        Assert.True(result.Found);
        Assert.False(result.Saved);
        Assert.Equal("same", result.Value);
        _games.Verify(g => g.TryUpdateAsync(It.IsAny<IGame>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_SavesWithTheTokenItRead()
    {
        var game = NewGame();
        ReadReturns(("etag-1", game));
        _games.Setup(g => g.TryUpdateAsync(game, "etag-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _updater.UpdateAsync(_code, Join(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.Saved);
        Assert.Equal(JoinOutcome.Joined, result.Value);
        _games.Verify(g => g.TryUpdateAsync(game, "etag-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(0, _conflicts());
    }

    [Fact]
    public async Task Update_ReappliesTheChangeToTheReloadedGameAfterAConflict()
    {
        var first = NewGame();
        var second = NewGame();
        ReadReturns(("etag-1", first), ("etag-2", second));
        _games.Setup(g => g.TryUpdateAsync(first, "etag-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _games.Setup(g => g.TryUpdateAsync(second, "etag-2", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var seen = new List<Game>();
        var playerId = Guid.NewGuid();
        using var activity = new Activity("test").Start();

        var result = await _updater.UpdateAsync(_code, game => { seen.Add(game); return Join(playerId)(game); }, CancellationToken.None);

        Assert.True(result.Saved);
        Assert.Equal([first, second], seen);
        Assert.Equal(playerId, second.PlayerTwo!.PlayerId);
        Assert.Equal(1, _conflicts());
        var conflict = Assert.Single(activity.Events);
        Assert.Equal(GamesTelemetry.UpdateConflictEvent, conflict.Name);
    }

    [Fact]
    public async Task Update_ThrowsAfterThreeConflictsInARow()
    {
        ReadReturns(("etag-1", NewGame()), ("etag-2", NewGame()), ("etag-3", NewGame()), ("etag-4", NewGame()));
        _games.Setup(g => g.TryUpdateAsync(It.IsAny<IGame>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var conflict = await Assert.ThrowsAsync<GameUpdateConflictException>(
            () => _updater.UpdateAsync(_code, Join(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(_code.Value, conflict.GameCode);
        _games.Verify(g => g.TryUpdateAsync(It.IsAny<IGame>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(GameUpdater.MaxAttempts));
        Assert.Equal(GameUpdater.MaxAttempts, _conflicts());
    }

    [Fact]
    public async Task Update_LetsADomainExceptionPropagateWithoutWriting()
    {
        ReadReturns(("etag-1", NewGame()));

        await Assert.ThrowsAsync<DomainException>(() => _updater.UpdateAsync<int>(
            _code, _ => throw new DomainException("refused"), CancellationToken.None));

        _games.Verify(g => g.TryUpdateAsync(It.IsAny<IGame>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Game NewGame() => GameFactory.Stored(GameState.New, withoutPlayerTwo: true);

    private static Func<Game, GameMutation<JoinOutcome>> Join(Guid playerId) => game =>
    {
        var outcome = game.Join(playerId, GameFactory.PlayerName(), Now);
        return outcome == JoinOutcome.Joined ? GameMutation.Save(outcome) : GameMutation.NoChange(outcome);
    };

    private void ReadReturns(params (string Token, Game Game)[] reads)
    {
        var sequence = _games.SetupSequence(g => g.GetForUpdateAsync(_code.Value, It.IsAny<CancellationToken>()));
        foreach (var (token, game) in reads)
        {
            sequence = sequence.ReturnsAsync(new GameForUpdate(game, token));
        }
    }
}
