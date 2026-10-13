using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Services;

namespace HexMaster.Battleship.Games.Tests.Services;

public class GameCodeGeneratorTests
{
    private readonly GameCodeGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsEightCharactersFromTheAlphabet()
    {
        var code = _generator.Generate();

        Assert.Matches("^[A-Z0-9]{8}$", code.Value);
    }

    [Fact]
    public void Generate_UsesTheWholeAlphabetOverManyDraws()
    {
        // 2,000 codes are 16,000 characters; each of the 36 symbols is expected about 444 times,
        // so a missing symbol means the alphabet is wrong, not bad luck.
        var seen = Enumerable.Range(0, 2_000)
            .SelectMany(_ => _generator.Generate().Value)
            .ToHashSet();

        Assert.Equal(GameCode.Alphabet.ToHashSet(), seen);
    }

    [Fact]
    public void Generate_ReturnsDifferentCodes()
    {
        var codes = Enumerable.Range(0, 1_000).Select(_ => _generator.Generate()).ToHashSet();

        Assert.Equal(1_000, codes.Count);
    }
}
