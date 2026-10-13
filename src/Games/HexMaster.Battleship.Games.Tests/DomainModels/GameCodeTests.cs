using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Tests.DomainModels;

public class GameCodeTests
{
    [Theory]
    [InlineData("K7Q2M9XA")]
    [InlineData("AAAAAAAA")]
    [InlineData("01234567")]
    public void Parse_AcceptsEightUppercaseLettersOrDigits(string value)
    {
        var code = GameCode.Parse(value);

        Assert.Equal(value, code.Value);
        Assert.Equal(value, code.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K7Q2M9X")]
    [InlineData("K7Q2M9XAB")]
    [InlineData("k7q2m9xa")]
    [InlineData("K7Q2-9XA")]
    [InlineData("K7Q2 9XA")]
    [InlineData("K7Q2M9XÄ")]
    public void Parse_RefusesAnythingElse(string? value)
    {
        Assert.False(GameCode.TryParse(value, out _));
        Assert.Throws<DomainException>(() => GameCode.Parse(value));
    }

    [Fact]
    public void Codes_WithTheSameValueAreEqual()
    {
        Assert.Equal(GameCode.Parse("K7Q2M9XA"), GameCode.Parse("K7Q2M9XA"));
    }

    [Fact]
    public void Default_HasAnEmptyValue()
    {
        Assert.Equal(string.Empty, default(GameCode).Value);
    }
}
