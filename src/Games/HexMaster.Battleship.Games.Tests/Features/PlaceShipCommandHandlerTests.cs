using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Features.PlaceShip;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.Games.Tests.Factories;
using HexMaster.Battleship.Games.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HexMaster.Battleship.Games.Tests.Features;

public class PlaceShipCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    private static readonly ShipPlacement Carrier = new(ShipKind.Carrier, new(1, 2), Orientation.Vertical);

    private readonly InMemoryGameRepository _games = new();
    private readonly PlaceShipCommandHandler _handler;

    public PlaceShipCommandHandlerTests()
    {
        var updater = new GameUpdater(_games, TelemetryFactory.Create(out _), NullLogger<GameUpdater>.Instance);
        _handler = new PlaceShipCommandHandler(updater, new FakeTimeProvider(Later));
    }

    [Fact]
    public async Task Handle_StoresThePlacementAndReturnsTheCallersView()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New, lastActivityOn: Now));
        var playerId = game.PlayerTwo!.PlayerId;

        var view = await _handler.Handle(new PlaceShipCommand(game.Code, playerId, Carrier), CancellationToken.None);

        Assert.Equal(playerId, view!.Me.PlayerId);
        Assert.Equal([new ShipView(ShipKind.Carrier, 1, 2, Orientation.Vertical)], view.Me.Ships);
        var stored = await _games.GetByCodeAsync(game.GameCode, CancellationToken.None);
        Assert.Equal([Carrier], stored!.PlayerTwo!.Ships);
        Assert.Equal(Later, stored.LastActivityOn);
    }

    [Fact]
    public async Task Handle_ReturnsNullForAnUnknownCode()
    {
        Assert.Null(await _handler.Handle(new PlaceShipCommand(GameFactory.Code(), Guid.NewGuid(), Carrier), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReturnsNullAndWritesNothingForSomeoneWhoDoesNotPlay()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New));
        var token = _games.TokenOf(game.GameCode);

        Assert.Null(await _handler.Handle(new PlaceShipCommand(game.Code, Guid.NewGuid(), Carrier), CancellationToken.None));
        Assert.Equal(token, _games.TokenOf(game.GameCode));
    }

    [Fact]
    public async Task Handle_LetsARefusalThroughWithoutAWrite()
    {
        var game = await StoreAsync(GameFactory.Stored(GameState.New));
        var token = _games.TokenOf(game.GameCode);
        var offBoard = Carrier with { Origin = new(1, 9) };

        var refused = await Assert.ThrowsAsync<PlacementRefusedException>(
            () => _handler.Handle(new PlaceShipCommand(game.Code, game.PlayerOne.PlayerId, offBoard), CancellationToken.None));

        Assert.Equal(PlacementRefusedReason.OutOfBoard, refused.Reason);
        Assert.Equal(token, _games.TokenOf(game.GameCode));
    }

    private async Task<Game> StoreAsync(Game game)
    {
        await _games.TryAddAsync(game, CancellationToken.None);
        return game;
    }
}
