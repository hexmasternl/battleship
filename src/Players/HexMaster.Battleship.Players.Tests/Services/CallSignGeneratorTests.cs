using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Services;

namespace HexMaster.Battleship.Players.Tests.Services;

public class CallSignGeneratorTests
{
    [Fact]
    public void Generate_ReturnsARankAndASurname()
    {
        var callSign = new CallSignGenerator().Generate();

        var parts = callSign.Split(' ');
        Assert.Equal(2, parts.Length);
        Assert.Contains(parts[0], CallSignGenerator.Ranks);
        Assert.Contains(parts[1], CallSignGenerator.Surnames);
    }

    [Fact]
    public void EveryCombination_IsAValidDisplayName()
    {
        var longest = CallSignGenerator.Ranks.Max(r => r.Length) + 1 + CallSignGenerator.Surnames.Max(s => s.Length);

        Assert.True(longest <= DisplayNameRules.MaxLength);
    }
}
