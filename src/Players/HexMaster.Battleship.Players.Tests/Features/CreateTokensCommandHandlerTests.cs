using System.Diagnostics.Metrics;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DomainModels;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Features.CreateTokens;
using HexMaster.Battleship.Players.Observability;
using HexMaster.Battleship.Players.Services;
using HexMaster.Battleship.Players.Tests.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.Players.Tests.Features;

public class CreateTokensCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, 750, TimeSpan.Zero);
    private static readonly DateTimeOffset NowInSeconds = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly List<IUser> _savedUsers = [];
    private readonly List<IRefreshToken> _savedTokens = [];
    private readonly CreateTokensCommandHandler _handler;

    public CreateTokensCommandHandlerTests()
    {
        _users.Setup(r => r.SaveAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .Callback<IUser, CancellationToken>((user, _) => _savedUsers.Add(user))
            .Returns(Task.CompletedTask);
        _refreshTokens.Setup(r => r.SaveAsync(It.IsAny<IRefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<IRefreshToken, CancellationToken>((token, _) => _savedTokens.Add(token))
            .Returns(Task.CompletedTask);

        _handler = new CreateTokensCommandHandler(
            _users.Object,
            _refreshTokens.Object,
            new JwtTokenService(Options.Create(TokenOptionsFactory.Create()), new RefreshTokenGenerator()),
            new CallSignGenerator(),
            new FakeTimeProvider(Now),
            new PlayersMetrics(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>()),
            NullLogger<CreateTokensCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_StoresANewUserWithTheGivenDisplayName()
    {
        await _handler.Handle(new CreateTokensCommand("Admiral Nelson"), CancellationToken.None);

        var user = Assert.Single(_savedUsers);
        Assert.Equal("Admiral Nelson", user.DisplayName);
        Assert.Equal(DomainModelState.New, user.State);
        Assert.Equal(NowInSeconds, user.CreatedAt);
        Assert.Equal(NowInSeconds, user.LastSeenAt);
        Assert.False(user.IsRevoked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_DefaultsTheDisplayNameToACallSign(string? displayName)
    {
        await _handler.Handle(new CreateTokensCommand(displayName), CancellationToken.None);

        var user = Assert.Single(_savedUsers);
        var parts = user.DisplayName.Split(' ');
        Assert.Contains(parts[0], CallSignGenerator.Ranks);
        Assert.Contains(parts[1], CallSignGenerator.Surnames);
    }

    [Fact]
    public async Task Handle_StoresOnlyTheHashOfTheReturnedRefreshToken()
    {
        var response = await _handler.Handle(new CreateTokensCommand("Admiral Nelson"), CancellationToken.None);

        var stored = Assert.Single(_savedTokens);
        Assert.Equal(RefreshTokenGenerator.Hash(response.RefreshToken), stored.Id);
        Assert.NotEqual(response.RefreshToken, stored.Id);
        Assert.Equal(_savedUsers[0].Id, stored.UserId);
        Assert.Equal(DomainModelState.New, stored.State);
    }

    [Fact]
    public async Task Handle_ReturnsTheExpiryInstantsOfBothTokens()
    {
        var response = await _handler.Handle(new CreateTokensCommand("Admiral Nelson"), CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.Equal(NowInSeconds.AddMinutes(15), response.AccessTokenExpiresAt);
        Assert.Equal(NowInSeconds.AddDays(90), response.RefreshTokenExpiresAt);
        Assert.Equal(_savedTokens[0].ExpiresAt, response.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task Handle_CreatesADifferentUserAndRefreshTokenEveryTime()
    {
        var first = await _handler.Handle(new CreateTokensCommand(null), CancellationToken.None);
        var second = await _handler.Handle(new CreateTokensCommand(null), CancellationToken.None);

        Assert.NotEqual(_savedUsers[0].Id, _savedUsers[1].Id);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    }

    [Fact]
    public async Task Handle_RejectsAnInvalidDisplayNameWithoutStoringAnything()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(new CreateTokensCommand(new string('x', 41)), CancellationToken.None));

        Assert.Empty(_savedUsers);
        Assert.Empty(_savedTokens);
    }
}
