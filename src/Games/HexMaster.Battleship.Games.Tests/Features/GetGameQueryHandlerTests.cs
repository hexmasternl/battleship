using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Features.GetGame;
using HexMaster.Battleship.Games.Tests.Factories;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Features;

public class GetGameQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _games = new(MockBehavior.Strict);

    private GetGameQueryHandler CreateHandler() => new(_games.Object);

    private void Store(IGame game) =>
        _games.Setup(g => g.GetByCodeAsync(game.GameCode, It.IsAny<CancellationToken>())).ReturnsAsync(game);

    [Fact]
    public void Mapper_ShowsTheCreatorAloneWithoutAnOpponent()
    {
        var game = Game.Create(Guid.NewGuid(), "Admiral Nelson", GameFactory.Code(), Now);

        var view = GameViewMapper.ToView(game, game.PlayerOne.PlayerId);

        Assert.Equal(game.Id, view.GameId);
        Assert.Equal(game.GameCode, view.GameCode);
        Assert.Equal(GameState.New, view.State);
        Assert.Equal(new PlayerView(game.PlayerOne.PlayerId, "Admiral Nelson", false, false, [], []), view.Me with { Ships = [], Shots = [] });
        Assert.Empty(view.Me.Ships);
        Assert.Empty(view.Me.Shots);
        Assert.Null(view.Opponent);
    }

    [Fact]
    public void Mapper_ShowsPlayerTwoAsMeAndPlayerOneAsTheOpponentWithoutShips()
    {
        var playerOne = GameFactory.Player(isReady: true, ships: GameFactory.FullFleet(), shots: [new(1, 2, true)]);
        var playerTwo = GameFactory.Player(
            ships: [new ShipPlacement(ShipKind.Carrier, new(0, 0), Orientation.Horizontal)], shots: [new(3, 7, false)]);
        var game = GameFactory.Stored(GameState.New, playerOne, playerTwo);

        var view = GameViewMapper.ToView(game, playerTwo.PlayerId);

        Assert.Equal(playerTwo.PlayerId, view.Me.PlayerId);
        Assert.Equal([new ShipView(ShipKind.Carrier, 0, 0, Orientation.Horizontal)], view.Me.Ships);
        Assert.Equal([new ShotView(3, 7, false)], view.Me.Shots);
        Assert.Equal(new OpponentView(playerOne.PlayerId, playerOne.PlayerName, true, false, view.Opponent!.Shots), view.Opponent);
        Assert.Equal([new ShotView(1, 2, true)], view.Opponent.Shots);
        Assert.DoesNotContain(typeof(OpponentView).GetProperties(), p => p.Name.Contains("Ship", StringComparison.Ordinal));
    }

    [Fact]
    public void Mapper_RefusesSomeoneWhoDoesNotPlay()
    {
        Assert.Throws<ArgumentException>(() => GameViewMapper.ToView(GameFactory.Stored(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Handle_ReturnsNullForAnUnknownCode()
    {
        var code = GameFactory.Code();
        _games.Setup(g => g.GetByCodeAsync(code.Value, It.IsAny<CancellationToken>())).ReturnsAsync((IGame?)null);

        Assert.Null(await CreateHandler().Handle(new GetGameQuery(code, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReturnsNullForSomeoneWhoDoesNotPlay()
    {
        var game = GameFactory.Stored(GameState.New);
        Store(game);

        Assert.Null(await CreateHandler().Handle(new GetGameQuery(game.Code, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReturnsTheCallersViewAndWritesNothing()
    {
        var game = GameFactory.Stored(GameState.New);
        Store(game);

        var view = await CreateHandler().Handle(new GetGameQuery(game.Code, game.PlayerTwo!.PlayerId), CancellationToken.None);

        Assert.Equal(game.PlayerTwo.PlayerId, view!.Me.PlayerId);
        Assert.Equal(game.PlayerOne.PlayerId, view.Opponent!.PlayerId);
        // The strict mock fails on any call other than the read.
        _games.Verify(g => g.GetByCodeAsync(game.GameCode, It.IsAny<CancellationToken>()), Times.Once);
        _games.VerifyNoOtherCalls();
    }
}
