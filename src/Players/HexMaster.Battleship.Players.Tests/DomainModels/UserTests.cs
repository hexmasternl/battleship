using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.DomainModels;
using HexMaster.Battleship.Players.Tests.Factories;

namespace HexMaster.Battleship.Players.Tests.DomainModels;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_MakesANewUserLastSeenWhenCreated()
    {
        var user = User.Create("Admiral Nelson", Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Admiral Nelson", user.DisplayName);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal(Now, user.LastSeenAt);
        Assert.False(user.IsRevoked);
        Assert.Equal(DomainModelState.New, user.State);
    }

    [Fact]
    public void Create_GivesEveryUserADifferentIdentifier()
    {
        Assert.NotEqual(User.Create("A", Now).Id, User.Create("A", Now).Id);
    }

    [Fact]
    public void Create_TrimsTheDisplayName()
    {
        Assert.Equal("Captain Hook", User.Create("  Captain Hook ", Now).DisplayName);
    }

    [Fact]
    public void Create_AcceptsFortyCharacters()
    {
        var name = new string('x', 40);

        Assert.Equal(name, User.Create(name, Now).DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_RejectsABlankDisplayName(string? displayName)
    {
        Assert.Throws<DomainException>(() => User.Create(displayName!, Now));
    }

    [Fact]
    public void Create_RejectsMoreThanFortyCharacters()
    {
        Assert.Throws<DomainException>(() => User.Create(new string('x', 41), Now));
    }

    [Fact]
    public void Reconcile_ProducesAPristineUserWithTheStoredValues()
    {
        var id = Guid.NewGuid();
        var created = Now.AddDays(-3);

        var user = User.Reconcile(id, "Commodore Perry", created, Now, true);

        Assert.Equal(DomainModelState.Pristine, user.State);
        Assert.Equal(id, user.Id);
        Assert.Equal("Commodore Perry", user.DisplayName);
        Assert.Equal(created, user.CreatedAt);
        Assert.Equal(Now, user.LastSeenAt);
        Assert.True(user.IsRevoked);
    }

    [Fact]
    public void Touch_RecordsTheLastSeenTime()
    {
        var user = UserFactory.Stored(createdAt: Now.AddDays(-1));

        user.Touch(Now);

        Assert.Equal(Now, user.LastSeenAt);
        Assert.Equal(DomainModelState.Modified, user.State);
    }

    [Fact]
    public void Revoke_MarksTheUserRevoked()
    {
        var user = UserFactory.Stored();

        user.Revoke();

        Assert.True(user.IsRevoked);
        Assert.Equal(DomainModelState.Modified, user.State);
    }
}
