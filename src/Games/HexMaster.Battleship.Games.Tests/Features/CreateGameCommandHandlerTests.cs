using HexMaster.Battleship.Events.Abstractions;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Features.CreateGame;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Features;

public class CreateGameCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _games = new();
    private readonly Mock<IGameCodeGenerator> _codes = new();
    private readonly Mock<IIntegrationEventSender> _events = new();
    private readonly Mock<ILogger<CreateGameCommandHandler>> _logger = new();
    private readonly List<IGame> _attempted = [];
    private readonly Func<long> _gamesCreated;
    private readonly CreateGameCommandHandler _handler;

    public CreateGameCommandHandlerTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _codes.Setup(c => c.Generate()).Returns(GameFactory.Code);
        _games.Setup(g => g.TryAddAsync(It.IsAny<IGame>(), It.IsAny<CancellationToken>()))
            .Callback<IGame, CancellationToken>((game, _) => _attempted.Add(game))
            .ReturnsAsync(true);

        var telemetry = TelemetryFactory.Create(out var meterFactory);
        _gamesCreated = TelemetryFactory.Listen(meterFactory, "games.created");

        _handler = new CreateGameCommandHandler(
            _games.Object, _codes.Object, _events.Object, telemetry, new FakeTimeProvider(Now), _logger.Object);
    }

    [Fact]
    public async Task Handle_StoresANewGameWithTheCallerAsPlayerOne()
    {
        var playerId = Guid.NewGuid();

        var response = await _handler.Handle(new CreateGameCommand(playerId, "Admiral Nelson"), CancellationToken.None);

        var stored = Assert.Single(_attempted);
        Assert.Equal(stored.Id, response.GameId);
        Assert.Equal(stored.GameCode, response.GameCode);
        Assert.Equal(GameState.New, stored.GameState);
        Assert.Equal(playerId, stored.PlayerOne.PlayerId);
        Assert.Equal("Admiral Nelson", stored.PlayerOne.PlayerName);
        Assert.False(stored.PlayerOne.HasTurn);
        Assert.Null(stored.PlayerTwo);
        Assert.Equal(Now, stored.LastActivityOn);
        Assert.Equal(1, _gamesCreated());
    }

    [Fact]
    public async Task Handle_TriesAnotherCodeWhenTheCodeIsInUse()
    {
        var taken = GameCode.Parse("TAKEN000");
        var free = GameCode.Parse("FREE0000");
        _codes.SetupSequence(c => c.Generate()).Returns(taken).Returns(free);
        _games.Setup(g => g.TryAddAsync(It.IsAny<IGame>(), It.IsAny<CancellationToken>()))
            .Callback<IGame, CancellationToken>((game, _) => _attempted.Add(game))
            .ReturnsAsync((IGame game, CancellationToken _) => game.GameCode != taken.Value);

        var response = await _handler.Handle(new CreateGameCommand(Guid.NewGuid(), "Admiral Nelson"), CancellationToken.None);

        Assert.Equal(free.Value, response.GameCode);
        Assert.Equal(new[] { taken.Value, free.Value }, _attempted.Select(g => g.GameCode));
        _events.Verify(e => e.SendAsync(
            It.Is<GameCreatedEvent>(created => created.GameCode == free.Value),
            It.IsAny<EventAudience>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FailsAfterFiveCodesInUse()
    {
        _games.Setup(g => g.TryAddAsync(It.IsAny<IGame>(), It.IsAny<CancellationToken>()))
            .Callback<IGame, CancellationToken>((game, _) => _attempted.Add(game))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new CreateGameCommand(Guid.NewGuid(), "Admiral Nelson"), CancellationToken.None));

        Assert.Equal(CreateGameCommandHandler.MaxCodeAttempts, _attempted.Count);
        _events.VerifyNoOtherCalls();
        Assert.Equal(0, _gamesCreated());
    }

    [Fact]
    public async Task Handle_AnnouncesTheGameToTheCreator()
    {
        var playerId = Guid.NewGuid();
        GameCreatedEvent? sent = null;
        EventAudience? audience = null;
        _events.Setup(e => e.SendAsync(It.IsAny<GameCreatedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .Callback<GameCreatedEvent, EventAudience, CancellationToken>((e, a, _) => (sent, audience) = (e, a))
            .Returns(Task.CompletedTask);

        var response = await _handler.Handle(new CreateGameCommand(playerId, "Admiral Nelson"), CancellationToken.None);

        Assert.Equal(new GameCreatedEvent(response.GameId, response.GameCode, playerId), sent);
        Assert.Equal(EventTarget.Player, audience!.Value.Target);
        Assert.Equal(new[] { playerId.ToString() }, audience.Value.TargetIds);
    }

    [Fact]
    public async Task Handle_SwallowsAndLogsAPublishFailure()
    {
        _events.Setup(e => e.SendAsync(It.IsAny<GameCreatedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The sidecar is down."));

        var response = await _handler.Handle(new CreateGameCommand(Guid.NewGuid(), "Admiral Nelson"), CancellationToken.None);

        Assert.Single(_attempted);
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(response.GameCode)),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LetsCancellationOfThePublishThrough()
    {
        _events.Setup(e => e.SendAsync(It.IsAny<GameCreatedEvent>(), It.IsAny<EventAudience>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.Handle(new CreateGameCommand(Guid.NewGuid(), "Admiral Nelson"), CancellationToken.None));
    }
}
