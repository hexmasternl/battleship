using System.Diagnostics.Metrics;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DomainModels;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Features.RefreshTokens;
using HexMaster.Battleship.Players.Observability;
using HexMaster.Battleship.Players.Services;
using HexMaster.Battleship.Players.Tests.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace HexMaster.Battleship.Players.Tests.Features;

public class RefreshTokensCommandHandlerTests
{
    private const string Presented = "presented-refresh-token";
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly List<IRefreshToken> _savedTokens = [];
    private readonly List<IUser> _savedUsers = [];
    private readonly RefreshTokensCommandHandler _handler;

    public RefreshTokensCommandHandlerTests()
    {
        _users.Setup(r => r.SaveAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .Callback<IUser, CancellationToken>((user, _) => _savedUsers.Add(user))
            .Returns(Task.CompletedTask);
        _refreshTokens.Setup(r => r.SaveAsync(It.IsAny<IRefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<IRefreshToken, CancellationToken>((token, _) => _savedTokens.Add(token))
            .Returns(Task.CompletedTask);

        _handler = new RefreshTokensCommandHandler(
            _users.Object,
            _refreshTokens.Object,
            new JwtTokenService(Options.Create(TokenOptionsFactory.Create()), new RefreshTokenGenerator()),
            new FakeTimeProvider(Now),
            new PlayersMetrics(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>()),
            NullLogger<RefreshTokensCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_RotatesTheTokenAndTouchesTheUser()
    {
        var user = UserFactory.Stored(createdAt: Now.AddDays(-5));
        var token = ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(Presented), user.Id));
        ArrangeUser(user);

        var response = await _handler.Handle(new RefreshTokensCommand(Presented), CancellationToken.None);

        Assert.Equal(user.Id.ToString(), new JsonWebToken(response.AccessToken).Subject);
        Assert.NotEqual(Presented, response.RefreshToken);
        Assert.Equal(Now.AddMinutes(15), response.AccessTokenExpiresAt);
        Assert.Equal(Now.AddDays(90), response.RefreshTokenExpiresAt);

        Assert.Equal(2, _savedTokens.Count);
        Assert.Same(token, _savedTokens[0]);
        Assert.Equal(Now, token.ConsumedAt);
        Assert.Equal(DomainModelState.Modified, token.State);

        var rotated = _savedTokens[1];
        Assert.Equal(RefreshTokenGenerator.Hash(response.RefreshToken), rotated.Id);
        Assert.Equal(user.Id, rotated.UserId);
        Assert.Equal(DomainModelState.New, rotated.State);

        Assert.Same(user, Assert.Single(_savedUsers));
        Assert.Equal(Now, user.LastSeenAt);
    }

    [Fact]
    public async Task UnknownToken_IsRefused()
    {
        await AssertRefusedAsync("unknown token");
    }

    [Fact]
    public async Task ExpiredToken_IsRefused()
    {
        var user = ArrangeUser(UserFactory.Stored());
        ArrangeToken(RefreshTokenFactory.Stored(
            Now, RefreshTokenGenerator.Hash(Presented), user.Id, issuedAt: Now.AddDays(-91), expiresAt: Now.AddDays(-1)));

        await AssertRefusedAsync("expired token");
    }

    [Fact]
    public async Task ConsumedToken_IsRefused()
    {
        var user = ArrangeUser(UserFactory.Stored());
        ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(Presented), user.Id, consumedAt: Now.AddMinutes(-1)));

        await AssertRefusedAsync("token used already");
    }

    [Fact]
    public async Task TokenOfAnUnknownUser_IsRefused()
    {
        ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(Presented)));

        await AssertRefusedAsync("unknown user");
    }

    [Fact]
    public async Task TokenOfARevokedUser_IsRefused()
    {
        var user = ArrangeUser(UserFactory.Stored(isRevoked: true));
        ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(Presented), user.Id));

        await AssertRefusedAsync("revoked user");
    }

    [Fact]
    public async Task TokenConsumedConcurrently_IsRefused()
    {
        var user = ArrangeUser(UserFactory.Stored());
        ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(Presented), user.Id));
        _refreshTokens.Setup(r => r.SaveAsync(It.IsAny<IRefreshToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("changed"));

        await AssertRefusedAsync("token used already");
        Assert.Empty(_savedUsers);
    }

    private async Task AssertRefusedAsync(string reason)
    {
        var exception = await Assert.ThrowsAsync<TokenRefusedException>(
            () => _handler.Handle(new RefreshTokensCommand(Presented), CancellationToken.None));

        Assert.Equal(reason, exception.Reason);
        Assert.DoesNotContain(_savedTokens, t => t.State == DomainModelState.New);
        Assert.Empty(_savedUsers);
    }

    private IRefreshToken ArrangeToken(IRefreshToken token)
    {
        _refreshTokens.Setup(r => r.GetAsync(token.Id, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        return token;
    }

    private IUser ArrangeUser(IUser user)
    {
        _users.Setup(r => r.GetAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
