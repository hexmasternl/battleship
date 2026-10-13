using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Sessions;
using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.DevBot.Tests.Factories;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.DevBot.Tests.Sessions;

public sealed class GameSessionTests
{
    private readonly string code = GameViewFactory.Code();
    private readonly Mock<IGamesClient> client = new();
    private readonly BotSettings settings = new(new DevBotOptions { ShotDelay = TimeSpan.Zero });
    private readonly GameSession session;

    public GameSessionTests()
    {
        session = new GameSession(code, client.Object, settings, new FakeTimeProvider(), new Random(3), NullLogger.Instance);
        client.Setup(c => c.PlaceShipAsync(code, It.IsAny<ShipPlacement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code)));
    }

    [Fact]
    public async Task ReconcileAsync_NoShips_PlacesAValidFleetAndGetsReady()
    {
        Read(GameViewFactory.View(code));
        client.Setup(c => c.ReadyAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code, ships: GameViewFactory.FullFleet(), ready: true)));

        await session.ReconcileAsync(CancellationToken.None);

        var placed = PlacedShips();
        Assert.Equal(Fleet.Kinds.Order(), placed.Select(p => p.Kind).Order());
        var cells = placed.SelectMany(p => p.Cells).ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(cells, c => Assert.True(Board.Contains(c)));
        client.Verify(c => c.ReadyAsync(code, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.FireAsync(It.IsAny<string>(), It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(session.IsEnded);
    }

    [Fact]
    public async Task ReconcileAsync_SomeShipsPlaced_PlacesOnlyTheMissingOnesAroundThem()
    {
        IReadOnlyList<ShipView> existing =
        [
            new(ShipKind.Carrier, 0, 0, Orientation.Horizontal),
            new(ShipKind.Destroyer, 0, 1, Orientation.Vertical),
        ];
        Read(GameViewFactory.View(code, ships: existing));
        client.Setup(c => c.ReadyAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code, ships: GameViewFactory.FullFleet(), ready: true)));

        await session.ReconcileAsync(CancellationToken.None);

        var placed = PlacedShips();
        Assert.Equal([ShipKind.Battleship, ShipKind.Cruiser, ShipKind.Submarine], placed.Select(p => p.Kind).Order());
        var taken = new[] { new Coordinate(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(0, 1), new(0, 2) };
        Assert.DoesNotContain(placed.SelectMany(p => p.Cells), taken.Contains);
    }

    [Fact]
    public async Task ReconcileAsync_PlacementRefusedOnce_ReadsAgainAndPlacesTheMissingShips()
    {
        Read(GameViewFactory.View(code));
        client.SetupSequence(c => c.PlaceShipAsync(code, It.IsAny<ShipPlacement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Problem(GameCallStatus.PlacementRefused, "overlap"))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code)))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code)))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code)))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code)))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code)));
        client.Setup(c => c.ReadyAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(GameViewFactory.View(code, ships: GameViewFactory.FullFleet(), ready: true)));

        await session.ReconcileAsync(CancellationToken.None);

        client.Verify(c => c.PlaceShipAsync(code, It.IsAny<ShipPlacement>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
        client.Verify(c => c.ReadyAsync(code, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_PlacementRefusedTwice_StopsWithoutGettingReady()
    {
        Read(GameViewFactory.View(code));
        client.Setup(c => c.PlaceShipAsync(code, It.IsAny<ShipPlacement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Problem(GameCallStatus.PlacementRefused, "overlap"));

        await session.ReconcileAsync(CancellationToken.None);

        client.Verify(c => c.ReadyAsync(code, It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(session.IsEnded);
    }

    [Fact]
    public async Task ReconcileAsync_ReadyButNotYetStarted_Waits()
    {
        Read(GameViewFactory.View(code, GameState.New, GameViewFactory.FullFleet(), ready: true));

        await session.ReconcileAsync(CancellationToken.None);

        client.Verify(c => c.ReadyAsync(code, It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.FireAsync(It.IsAny<string>(), It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_StartedWithoutTheTurn_DoesNotFire()
    {
        Read(GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: false));

        await session.ReconcileAsync(CancellationToken.None);

        client.Verify(c => c.FireAsync(It.IsAny<string>(), It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_HasTheTurn_FiresOnceAtAnUnshotCellWithTheActivePreset()
    {
        settings.Intelligence = BotIntelligence.HuntAndTarget;
        Read(GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: true,
            shots: [ShotFactory.Miss(0, 0), ShotFactory.Hit(5, 5)]));
        client.Setup(c => c.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Coordinate at, CancellationToken _) =>
                GameCallResult<FireShotResponse>.Ok(new FireShotResponse(at.Column, at.Row, false, GameState.Started)));

        await session.ReconcileAsync(CancellationToken.None);

        var invocation = Assert.Single(client.Invocations, i => i.Method.Name == nameof(IGamesClient.FireAsync));
        var at = (Coordinate)invocation.Arguments[1]!;
        Assert.Contains(at, new Coordinate[] { new(4, 5), new(6, 5), new(5, 4), new(5, 6) });
        Assert.False(session.IsEnded);
    }

    [Fact]
    public async Task ReconcileAsync_ShotRefusedWithConflict_ReadsAgainInsteadOfRetrying()
    {
        client.SetupSequence(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(
                GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: true)))
            .ReturnsAsync(GameCallResult<GameView>.Ok(
                GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: false)));
        client.Setup(c => c.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<FireShotResponse>.Problem(GameCallStatus.NotYourTurn, "The other player has the turn."));

        await session.ReconcileAsync(CancellationToken.None);

        client.Verify(c => c.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.False(session.IsEnded);
    }

    [Fact]
    public async Task ReconcileAsync_GameEndedBeforeTheShot_ReadsTheCompletedGameAndStops()
    {
        // game-ending: a completed game refuses shots with 409; the re-read shows the winner.
        client.SetupSequence(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Ok(
                GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: true)))
            .ReturnsAsync(GameCallResult<GameView>.Ok(
                GameViewFactory.View(code, GameState.Completed, GameViewFactory.FullFleet(), ready: true, winnerId: Guid.NewGuid())));
        client.Setup(c => c.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<FireShotResponse>.Problem(GameCallStatus.GameNotStarted, "The game is not being played."));

        await session.ReconcileAsync(CancellationToken.None);

        Assert.True(session.IsEnded);
        client.Verify(c => c.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_ShotWinsTheGame_EndsTheSession()
    {
        Read(GameViewFactory.View(code, GameState.Started, GameViewFactory.FullFleet(), ready: true, hasTurn: true));
        client.Setup(c => c.FireAsync(code, It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<FireShotResponse>.Ok(new FireShotResponse(1, 1, true, GameState.Completed)));

        await session.ReconcileAsync(CancellationToken.None);

        Assert.True(session.IsEnded);
    }

    [Theory]
    [InlineData(GameState.Completed)]
    [InlineData(GameState.Abandoned)]
    public async Task ReconcileAsync_GameOver_EndsTheSessionWithoutFiring(GameState state)
    {
        Read(GameViewFactory.View(code, state, GameViewFactory.FullFleet(), ready: true, winnerId: Guid.NewGuid()));

        await session.ReconcileAsync(CancellationToken.None);

        Assert.True(session.IsEnded);
        client.Verify(c => c.FireAsync(It.IsAny<string>(), It.IsAny<Coordinate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_GameNotFound_EndsTheSession()
    {
        client.Setup(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Problem(GameCallStatus.NotFound, "gone"));

        await session.ReconcileAsync(CancellationToken.None);
        await session.ReconcileAsync(CancellationToken.None);

        Assert.True(session.IsEnded);
        client.Verify(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_ReadFails_KeepsTheSession()
    {
        client.Setup(c => c.GetGameAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameCallResult<GameView>.Problem(GameCallStatus.Failed, "boom"));

        await session.ReconcileAsync(CancellationToken.None);

        Assert.False(session.IsEnded);
    }

    private void Read(GameView view) =>
        client.Setup(c => c.GetGameAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(GameCallResult<GameView>.Ok(view));

    private List<ShipPlacement> PlacedShips() =>
    [
        .. client.Invocations
            .Where(i => i.Method.Name == nameof(IGamesClient.PlaceShipAsync))
            .Select(i => (ShipPlacement)i.Arguments[1]!),
    ];
}
