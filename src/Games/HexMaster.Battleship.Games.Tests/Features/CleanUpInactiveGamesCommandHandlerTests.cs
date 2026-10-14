using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Features.CleanUpInactiveGames;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.Games.Tests.Fakes;
using HexMaster.Battleship.Games.Tests.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace HexMaster.Battleship.Games.Tests.Features;

public class CleanUpInactiveGamesCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 3, 0, 0, TimeSpan.Zero);

    private readonly InMemoryGameRepository _games = new();
    private readonly FakeLogger<CleanUpInactiveGamesCommandHandler> _logger = new();
    private readonly Func<long> _gamesRemoved;
    private readonly GamesMetrics _metrics;

    public CleanUpInactiveGamesCommandHandlerTests()
    {
        _metrics = TelemetryFactory.Create(out var meterFactory);
        _gamesRemoved = TelemetryFactory.Listen(meterFactory, GamesMetrics.GamesRemovedName);
    }

    [Fact]
    public async Task Handle_RemovesGamesInactiveForNinetyOneDaysAndKeepsThoseOfEightyNine()
    {
        var old = await StoreAsync(Now.AddDays(-91));
        var recent = await StoreAsync(Now.AddDays(-89));

        var result = await Handler().Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(new CleanUpInactiveGamesResult(1, 0), result);
        Assert.False(_games.Contains(old.GameCode));
        Assert.True(_games.Contains(recent.GameCode));
    }

    [Fact]
    public async Task Handle_KeepsAGameExactlyAtTheRetention()
    {
        var game = await StoreAsync(Now.AddDays(-90));

        var result = await Handler().Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(0, result.Removed);
        Assert.True(_games.Contains(game.GameCode));
    }

    [Fact]
    public async Task Handle_RemovesInactiveGamesInEveryState()
    {
        var stored = new List<IGame>();
        foreach (var state in Enum.GetValues<GameState>())
        {
            stored.Add(await StoreAsync(Now.AddDays(-120), state));
        }

        var result = await Handler().Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(4, result.Removed);
        Assert.All(stored, game => Assert.False(_games.Contains(game.GameCode)));
    }

    [Fact]
    public async Task Handle_RemovesAGameWhoseStoredDocumentCannotBeRead()
    {
        var code = GameFactory.Code().Value;
        _games.AddUnreadable(code, Now.AddDays(-120));

        var result = await Handler().Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(1, result.Removed);
        Assert.False(_games.Contains(code));
    }

    [Fact]
    public async Task Handle_UsesTheConfiguredRetention()
    {
        var old = await StoreAsync(Now.AddDays(-31));
        var recent = await StoreAsync(Now.AddDays(-29));

        var result = await Handler(inactiveDays: 30).Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(1, result.Removed);
        Assert.False(_games.Contains(old.GameCode));
        Assert.True(_games.Contains(recent.GameCode));
    }

    [Fact]
    public async Task Handle_ASecondRunRemovesNothing()
    {
        await StoreAsync(Now.AddDays(-100));
        await StoreAsync(Now.AddDays(-200));
        var handler = Handler();

        var first = await handler.Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);
        var second = await handler.Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(new CleanUpInactiveGamesResult(2, 0), first);
        Assert.Equal(new CleanUpInactiveGamesResult(0, 0), second);
    }

    [Fact]
    public async Task Handle_KeepsAGameChangedAfterItWasSelectedAndCountsItSkipped()
    {
        var game = await StoreAsync(Now.AddDays(-100), GameState.New, withoutPlayerTwo: true);
        var playerTwoId = Guid.NewGuid();
        _games.AfterSelect = async inactive =>
        {
            var read = await _games.GetForUpdateAsync(inactive.GameCode, CancellationToken.None);
            ((Game)read!.Game).Join(playerTwoId, GameFactory.PlayerName(), Now);
            Assert.True(await _games.TryUpdateAsync(read.Game, read.ConcurrencyToken, CancellationToken.None));
        };

        var result = await Handler().Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(new CleanUpInactiveGamesResult(0, 1), result);
        var stored = await _games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal(playerTwoId, stored!.PlayerTwo!.PlayerId);
    }

    [Fact]
    public async Task Handle_CountsAGameAlreadyGoneAsSkipped()
    {
        await StoreAsync(Now.AddDays(-100));
        _games.AfterSelect = inactive =>
            _games.TryDeleteAsync(inactive.GameCode, inactive.ConcurrencyToken, CancellationToken.None);

        var result = await Handler().Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(new CleanUpInactiveGamesResult(0, 1), result);
    }

    [Fact]
    public async Task Handle_CountsRemovedGamesInTheMetricAndLogsOneLine()
    {
        await StoreAsync(Now.AddDays(-100));
        await StoreAsync(Now.AddDays(-150));
        await StoreAsync(Now.AddDays(-365));
        await StoreAsync(Now.AddDays(-1));

        await Handler().Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        Assert.Equal(3, _gamesRemoved());
        var log = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(4012, log.Id.Id);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("Removed 3 inactive games (skipped 0)", log.Message);
    }

    [Fact]
    public async Task AddGamesModule_RunsTheCleanupInsideItsUseCaseSpan()
    {
        await StoreAsync(Now.AddDays(-100));
        await StoreAsync(Now.AddDays(-100));
        var configuration = new ConfigurationBuilder().Build();
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddGamesModule()
            .AddSingleton<IGameRepository>(_games)
            .AddSingleton<TimeProvider>(new FakeTimeProvider(Now))
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<CleanUpInactiveGamesCommand, CleanUpInactiveGamesResult>>();
        using var activities = new ActivityCollector();

        await handler.Handle(new CleanUpInactiveGamesCommand(), CancellationToken.None);

        var span = activities.Single("CleanUpInactiveGames");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(Now.AddDays(-90).ToString("O"), span.GetTagItem(GamesTelemetry.RetentionCutoffAttribute));
        Assert.Equal(2, span.GetTagItem(GamesTelemetry.RetentionRemovedAttribute));
        Assert.Equal(0, span.GetTagItem(GamesTelemetry.RetentionSkippedAttribute));
    }

    private CleanUpInactiveGamesCommandHandler Handler(int inactiveDays = 90) =>
        new(
            _games,
            Options.Create(new GameRetentionOptions { InactiveDays = inactiveDays }),
            _metrics,
            new FakeTimeProvider(Now),
            _logger);

    private async Task<IGame> StoreAsync(
        DateTimeOffset lastActivityOn, GameState state = GameState.Started, bool withoutPlayerTwo = false)
    {
        var game = GameFactory.Stored(state, withoutPlayerTwo: withoutPlayerTwo, lastActivityOn: lastActivityOn);
        Assert.True(await _games.TryAddAsync(game, CancellationToken.None));
        return game;
    }
}
