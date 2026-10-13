using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.DomainModels;
using HexMaster.Battleship.Players.Tests.Factories;

namespace HexMaster.Battleship.Players.Tests.DomainModels;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_MakesANewActiveTokenExpiringAfterTheLifetime()
    {
        var userId = Guid.NewGuid();

        var token = RefreshToken.Create("hash", userId, Now, TimeSpan.FromDays(90));

        Assert.Equal("hash", token.Id);
        Assert.Equal(userId, token.UserId);
        Assert.Equal(Now, token.IssuedAt);
        Assert.Equal(Now.AddDays(90), token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
        Assert.True(token.IsActive(Now));
        Assert.Equal(DomainModelState.New, token.State);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsAMissingHash(string hash)
    {
        Assert.Throws<DomainException>(() => RefreshToken.Create(hash, Guid.NewGuid(), Now, TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Create_RejectsAnEmptyUserId()
    {
        Assert.Throws<DomainException>(() => RefreshToken.Create("hash", Guid.Empty, Now, TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Create_RejectsANonPositiveLifetime()
    {
        Assert.Throws<DomainException>(() => RefreshToken.Create("hash", Guid.NewGuid(), Now, TimeSpan.Zero));
    }

    [Fact]
    public void Reconcile_ProducesAPristineTokenWithTheStoredValues()
    {
        var userId = Guid.NewGuid();
        var issued = Now.AddDays(-2);
        var consumed = Now.AddDays(-1);

        var token = RefreshToken.Reconcile("hash", userId, issued, issued.AddDays(90), consumed);

        Assert.Equal(DomainModelState.Pristine, token.State);
        Assert.Equal("hash", token.Id);
        Assert.Equal(userId, token.UserId);
        Assert.Equal(issued, token.IssuedAt);
        Assert.Equal(issued.AddDays(90), token.ExpiresAt);
        Assert.Equal(consumed, token.ConsumedAt);
    }

    [Fact]
    public void IsActive_IsFalseOnceExpired()
    {
        var token = RefreshTokenFactory.Stored(Now, expiresAt: Now);

        Assert.False(token.IsActive(Now));
        Assert.True(token.IsActive(Now.AddTicks(-1)));
    }

    [Fact]
    public void IsActive_IsFalseOnceConsumed()
    {
        var token = RefreshTokenFactory.Stored(Now, consumedAt: Now.AddMinutes(-1));

        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public void Consume_MarksAnActiveTokenAsUsed()
    {
        var token = RefreshTokenFactory.Stored(Now);

        token.Consume(Now);

        Assert.Equal(Now, token.ConsumedAt);
        Assert.False(token.IsActive(Now));
        Assert.Equal(DomainModelState.Modified, token.State);
    }

    [Fact]
    public void Consume_ThrowsWhenTheTokenWasConsumedAlready()
    {
        var token = RefreshTokenFactory.Stored(Now, consumedAt: Now.AddMinutes(-1));

        Assert.Throws<DomainException>(() => token.Consume(Now));
        Assert.Equal(DomainModelState.Pristine, token.State);
    }

    [Fact]
    public void Consume_ThrowsWhenTheTokenHasExpired()
    {
        var token = RefreshTokenFactory.Stored(Now, issuedAt: Now.AddDays(-91), expiresAt: Now.AddDays(-1));

        Assert.Throws<DomainException>(() => token.Consume(Now));
        Assert.Null(token.ConsumedAt);
    }
}
