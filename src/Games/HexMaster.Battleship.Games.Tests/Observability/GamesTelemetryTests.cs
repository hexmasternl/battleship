using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Core.Observability;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.Features.CreateGame;
using HexMaster.Battleship.Games.Observability;
using HexMaster.Battleship.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HexMaster.Battleship.Games.Tests.Observability;

public class GamesTelemetryTests
{
    [Fact]
    public async Task AddGamesModule_RunsCreateGameInsideItsUseCaseSpan()
    {
        var games = new Mock<IGameRepository>();
        games.Setup(g => g.TryAddAsync(It.IsAny<IGame>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddGamesModule()
            .AddSingleton(games.Object)
            .AddSingleton(Mock.Of<IIntegrationEventSender>())
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateGameCommand, CreateGameResponse>>();
        var playerId = Guid.NewGuid();
        using var activities = new ActivityCollector();

        var response = await handler.Handle(new CreateGameCommand(playerId, "Admiral Nelson"), CancellationToken.None);

        var decorator = Assert.IsType<InstrumentedCommandHandler<CreateGameCommand, CreateGameResponse>>(handler);
        Assert.IsType<CreateGameCommandHandler>(decorator.Inner);
        var span = activities.Single("CreateGame");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(playerId.ToString(), span.GetTagItem(GamesTelemetry.PlayerIdAttribute));
        Assert.Equal(response.GameId.ToString(), span.GetTagItem(GamesTelemetry.GameIdAttribute));
        Assert.Equal(response.GameCode, span.GetTagItem(GamesTelemetry.GameCodeAttribute));
        Assert.Equal(1, span.GetTagItem(GamesTelemetry.CodeAttemptsAttribute));
        Assert.DoesNotContain(span.TagObjects, t => Equals(t.Value, "Admiral Nelson"));
    }
}
