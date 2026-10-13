using System.Diagnostics;
using System.Diagnostics.Metrics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Core.Observability;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Abstractions.DomainModels;
using HexMaster.Battleship.Players.Abstractions.Repositories;
using HexMaster.Battleship.Players.Features.CreateTokens;
using HexMaster.Battleship.Players.Features.RefreshTokens;
using HexMaster.Battleship.Players.Observability;
using HexMaster.Battleship.Players.Services;
using HexMaster.Battleship.Players.Tests.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.Battleship.Players.Tests.Observability;

// Runs the Players handlers through the use-case decorator and checks the spans, metrics and logs
// they emit, including that no token or display name leaks into any of them.
public sealed class PlayersTelemetryTests : IDisposable
{
    private const string Presented = "presented-refresh-token";
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly List<IUser> _savedUsers = [];
    private readonly IMeterFactory _meterFactory =
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
    private readonly FakeLogCollector _logs = new();
    private readonly MetricCollector<long> _issued;
    private readonly MetricCollector<long> _refreshed;
    private readonly MetricCollector<long> _refused;
    private readonly MetricCollector<double> _duration;

    public PlayersTelemetryTests()
    {
        _users.Setup(r => r.SaveAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .Callback<IUser, CancellationToken>((user, _) => _savedUsers.Add(user))
            .Returns(Task.CompletedTask);
        _issued = new(_meterFactory, PlayersTelemetry.Name, PlayersMetrics.TokensIssuedName);
        _refreshed = new(_meterFactory, PlayersTelemetry.Name, PlayersMetrics.TokensRefreshedName);
        _refused = new(_meterFactory, PlayersTelemetry.Name, PlayersMetrics.TokensRefusedName);
        _duration = new(_meterFactory, CoreTelemetry.Name, CoreMetrics.UseCaseDurationName);
    }

    [Fact]
    public async Task CreateTokens_CountsTheIssueAndTagsTheSpanWithThePlayer()
    {
        using var activities = new ActivityCollector();

        await CreateTokens().Handle(new CreateTokensCommand("Admiral Nelson"), CancellationToken.None);

        var span = activities.Single("CreateTokens");
        var user = Assert.Single(_savedUsers);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(user.Id.ToString(), span.GetTagItem(PlayersTelemetry.PlayerIdAttribute));
        Assert.Equal(1, Assert.Single(_issued.GetMeasurementSnapshot()).Value);
        Assert.Contains(_logs.GetSnapshot(), l => l.Id.Id == 1000);
    }

    [Fact]
    public async Task RefreshTokens_CountsTheRefreshAndTagsTheSpanWithThePlayer()
    {
        using var activities = new ActivityCollector();
        var user = ArrangeUser(UserFactory.Stored());
        ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(Presented), user.Id));

        await RefreshTokens().Handle(new RefreshTokensCommand(Presented), CancellationToken.None);

        var span = activities.Single("RefreshTokens");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(user.Id.ToString(), span.GetTagItem(PlayersTelemetry.PlayerIdAttribute));
        Assert.Equal(1, Assert.Single(_refreshed.GetMeasurementSnapshot()).Value);
        Assert.Empty(_refused.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task UnknownToken_IsCountedAsRefusedByReasonAndEndsTheSpanInError()
    {
        using var activities = new ActivityCollector();

        await Assert.ThrowsAsync<TokenRefusedException>(
            () => RefreshTokens().Handle(new RefreshTokensCommand(Presented), CancellationToken.None));

        var span = activities.Single("RefreshTokens");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(TokenRefusalReasons.UnknownToken, span.GetTagItem(PlayersTelemetry.RefusalReasonAttribute));
        var refused = Assert.Single(_refused.GetMeasurementSnapshot());
        Assert.Equal(TokenRefusalReasons.UnknownToken, refused.Tags[PlayersTelemetry.ReasonTag]);
        Assert.Empty(_refreshed.GetMeasurementSnapshot());
        var duration = Assert.Single(_duration.GetMeasurementSnapshot());
        Assert.Equal(CoreTelemetry.FailureOutcome, duration.Tags[CoreTelemetry.OutcomeTag]);
    }

    [Fact]
    public async Task RevokedUser_IsCountedAsRefusedWithItsReason()
    {
        var user = ArrangeUser(UserFactory.Stored(isRevoked: true));
        ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(Presented), user.Id));

        await Assert.ThrowsAsync<TokenRefusedException>(
            () => RefreshTokens().Handle(new RefreshTokensCommand(Presented), CancellationToken.None));

        var refused = Assert.Single(_refused.GetMeasurementSnapshot());
        Assert.Equal(TokenRefusalReasons.RevokedUser, refused.Tags[PlayersTelemetry.ReasonTag]);
    }

    [Theory]
    [InlineData("unknown token", TokenRefusalReasons.UnknownToken)]
    [InlineData("expired token", TokenRefusalReasons.ExpiredToken)]
    [InlineData("token used already", TokenRefusalReasons.TokenUsedAlready)]
    [InlineData("unknown user", TokenRefusalReasons.UnknownUser)]
    [InlineData("revoked user", TokenRefusalReasons.RevokedUser)]
    [InlineData("something new", TokenRefusalReasons.Other)]
    public void RefusalReasons_FormAClosedSet(string reason, string expected)
    {
        Assert.Equal(expected, TokenRefusalReasons.Of(reason));
    }

    [Fact]
    public async Task NoSignal_CarriesATokenOrADisplayName()
    {
        using var activities = new ActivityCollector();
        const string displayName = "Admiral Nelson";

        var created = await CreateTokens().Handle(new CreateTokensCommand(displayName), CancellationToken.None);
        var user = Assert.Single(_savedUsers);
        ArrangeUser(user);
        ArrangeToken(RefreshTokenFactory.Stored(Now, RefreshTokenGenerator.Hash(created.RefreshToken), user.Id));
        var refreshed = await RefreshTokens().Handle(new RefreshTokensCommand(created.RefreshToken), CancellationToken.None);
        await Assert.ThrowsAsync<TokenRefusedException>(
            () => RefreshTokens().Handle(new RefreshTokensCommand(Presented), CancellationToken.None));

        string[] secrets =
        [
            displayName, created.AccessToken, created.RefreshToken, refreshed.AccessToken, refreshed.RefreshToken, Presented,
            RefreshTokenGenerator.Hash(created.RefreshToken), RefreshTokenGenerator.Hash(Presented),
        ];
        var emitted = activities.Activities
            .SelectMany(a => a.TagObjects.Select(t => t.Value?.ToString())
                .Concat(a.Events.SelectMany(e => e.Tags.Select(t => t.Value?.ToString())))
                .Append(a.DisplayName)
                .Append(a.StatusDescription))
            .Concat(new[] { _issued, _refreshed, _refused }
                .SelectMany(c => c.GetMeasurementSnapshot())
                .SelectMany(m => m.Tags.Select(t => t.Key + "=" + t.Value)))
            .Concat(_duration.GetMeasurementSnapshot().SelectMany(m => m.Tags.Select(t => t.Key + "=" + t.Value)))
            .Concat(_logs.GetSnapshot().SelectMany(l => (l.StructuredState ?? []).Select(s => s.Value).Append(l.Message)))
            .Where(value => !string.IsNullOrEmpty(value))
            .ToList();

        Assert.NotEmpty(emitted);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(emitted, value => value!.Contains(secret, StringComparison.Ordinal));
        }

        // A user id may appear on spans and in logs, but never as a metric tag.
        var metricTags = new[] { _issued, _refreshed, _refused }
            .SelectMany(c => c.GetMeasurementSnapshot())
            .SelectMany(m => m.Tags.Values.Select(v => v?.ToString()));
        Assert.DoesNotContain(metricTags, value => value == user.Id.ToString());
    }

    public void Dispose()
    {
        _issued.Dispose();
        _refreshed.Dispose();
        _refused.Dispose();
        _duration.Dispose();
    }

    private ICommandHandler<CreateTokensCommand, TokenPairResponse> CreateTokens() =>
        new InstrumentedCommandHandler<CreateTokensCommand, TokenPairResponse>(
            new CreateTokensCommandHandler(
                _users.Object,
                _refreshTokens.Object,
                TokenService(),
                new CallSignGenerator(),
                new FakeTimeProvider(Now),
                new PlayersMetrics(_meterFactory),
                new FakeLogger<CreateTokensCommandHandler>(_logs)),
            new CoreMetrics(_meterFactory),
            new FakeLogger(_logs));

    private ICommandHandler<RefreshTokensCommand, TokenPairResponse> RefreshTokens() =>
        new InstrumentedCommandHandler<RefreshTokensCommand, TokenPairResponse>(
            new RefreshTokensCommandHandler(
                _users.Object,
                _refreshTokens.Object,
                TokenService(),
                new FakeTimeProvider(Now),
                new PlayersMetrics(_meterFactory),
                new FakeLogger<RefreshTokensCommandHandler>(_logs)),
            new CoreMetrics(_meterFactory),
            new FakeLogger(_logs));

    private static JwtTokenService TokenService() =>
        new(Options.Create(TokenOptionsFactory.Create()), new RefreshTokenGenerator());

    private void ArrangeToken(IRefreshToken token) =>
        _refreshTokens.Setup(r => r.GetAsync(token.Id, It.IsAny<CancellationToken>())).ReturnsAsync(token);

    private IUser ArrangeUser(IUser user)
    {
        _users.Setup(r => r.GetAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
