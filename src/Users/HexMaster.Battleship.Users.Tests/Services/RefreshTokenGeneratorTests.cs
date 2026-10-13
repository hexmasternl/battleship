using System.Buffers.Text;
using HexMaster.Battleship.Users.Services;

namespace HexMaster.Battleship.Users.Tests.Services;

public class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _generator = new();

    [Fact]
    public void Generate_Returns256BitsBase64UrlEncoded()
    {
        var token = _generator.Generate();

        Assert.Equal(32, Base64Url.DecodeFromChars(token).Length);
        Assert.DoesNotContain('=', token);
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
    }

    [Fact]
    public void Generate_ReturnsADifferentValueEveryTime()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => _generator.Generate()).ToHashSet();

        Assert.Equal(100, tokens.Count);
    }

    [Fact]
    public void Hash_IsStableAndNeverEqualsTheRawValue()
    {
        var token = _generator.Generate();

        var hash = RefreshTokenGenerator.Hash(token);

        Assert.NotEqual(token, hash);
        Assert.Equal(hash, RefreshTokenGenerator.Hash(token));
        Assert.Equal(32, Base64Url.DecodeFromChars(hash).Length);
    }

    [Fact]
    public void Hash_DiffersForDifferentValues()
    {
        Assert.NotEqual(RefreshTokenGenerator.Hash("a"), RefreshTokenGenerator.Hash("b"));
    }
}
