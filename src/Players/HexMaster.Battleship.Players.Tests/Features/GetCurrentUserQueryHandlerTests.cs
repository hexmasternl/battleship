using HexMaster.Battleship.Players.Abstractions.DomainModels;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Features.GetCurrentUser;
using HexMaster.Battleship.Players.Tests.Factories;
using Moq;

namespace HexMaster.Battleship.Players.Tests.Features;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();

    [Fact]
    public async Task Handle_ReturnsTheIdAndDisplayNameOfTheUser()
    {
        var user = UserFactory.Stored(displayName: "Captain Hook");
        Arrange(user.Id, user);

        var response = await CreateHandler().Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(user.Id, response.Id);
        Assert.Equal("Captain Hook", response.DisplayName);
    }

    [Fact]
    public async Task Handle_ReturnsNullForAnUnknownUser()
    {
        var id = Guid.NewGuid();
        Arrange(id, null);

        Assert.Null(await CreateHandler().Handle(new GetCurrentUserQuery(id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReturnsNullForARevokedUser()
    {
        var user = UserFactory.Stored(isRevoked: true);
        Arrange(user.Id, user);

        Assert.Null(await CreateHandler().Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None));
    }

    private void Arrange(Guid id, IUser? user) =>
        _users.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    private GetCurrentUserQueryHandler CreateHandler() => new(_users.Object);
}
