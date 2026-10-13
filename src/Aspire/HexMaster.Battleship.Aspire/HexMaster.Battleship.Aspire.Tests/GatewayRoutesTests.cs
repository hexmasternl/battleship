using HexMaster.Battleship.Aspire.AppHost;
using HexMaster.Battleship.Aspire.Tests.Factories;

namespace HexMaster.Battleship.Aspire.Tests;

public sealed class GatewayRoutesTests
{
    [Fact]
    public void Modules_IsValid()
    {
        GatewayRoutes.Validate(GatewayRoutes.Modules);
    }

    [Theory]
    [InlineData("players", GatewayRoutes.PlayersApi)]
    [InlineData("games", GatewayRoutes.GamesApi)]
    public void Modules_RoutesPrefixToItsApi(string prefix, string apiResourceName)
    {
        var owner = Assert.Single(GatewayRoutes.Modules, m => m.Prefixes.Contains(prefix));

        Assert.Equal(apiResourceName, owner.ApiResourceName);
    }

    [Fact]
    public void Modules_DeclaresExactlyTheApprovedPrefixes()
    {
        var prefixes = GatewayRoutes.Modules.SelectMany(m => m.Prefixes).Order().ToArray();

        Assert.Equal(["games", "players"], prefixes);
    }

    [Fact]
    public void RoutePath_IsPrefixedCatchAll()
    {
        Assert.Equal("/players/{**catch-all}", GatewayRoutes.RoutePath("players"));
    }

    [Fact]
    public void Validate_AcceptsModuleWithSeveralPrefixes()
    {
        var module = ModuleRouteFactory.Create(prefixes: [ModuleRouteFactory.Prefix(), ModuleRouteFactory.Prefix()]);

        GatewayRoutes.Validate([module]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Players")]
    [InlineData("players/me")]
    [InlineData("-players")]
    [InlineData("pla yers")]
    public void Validate_RejectsPrefixThatIsNotOneLowercaseSegment(string prefix)
    {
        var module = ModuleRouteFactory.Create(prefixes: [prefix]);

        var exception = Assert.Throws<InvalidOperationException>(() => GatewayRoutes.Validate([module]));

        Assert.Contains(module.Module, exception.Message);
    }

    [Theory]
    [InlineData("smoke")]
    [InlineData("dapr")]
    [InlineData("health")]
    [InlineData("alive")]
    [InlineData("integration-events")]
    [InlineData("openapi")]
    [InlineData("weatherforecast")]
    public void Validate_RejectsReservedPrefix(string prefix)
    {
        var module = ModuleRouteFactory.Create(prefixes: [prefix]);

        var exception = Assert.Throws<InvalidOperationException>(() => GatewayRoutes.Validate([module]));

        Assert.Contains("reserved", exception.Message);
    }

    [Fact]
    public void Validate_RejectsPrefixDeclaredByTwoModules()
    {
        var prefix = ModuleRouteFactory.Prefix();
        var first = ModuleRouteFactory.Create(prefixes: [prefix]);
        var second = ModuleRouteFactory.Create(prefixes: [prefix]);

        var exception = Assert.Throws<InvalidOperationException>(() => GatewayRoutes.Validate([first, second]));

        Assert.Contains(first.Module, exception.Message);
        Assert.Contains(second.Module, exception.Message);
    }

    [Fact]
    public void Validate_RejectsModuleWithoutPrefixes()
    {
        var module = ModuleRouteFactory.Create(prefixes: []);

        Assert.Throws<InvalidOperationException>(() => GatewayRoutes.Validate([module]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_RejectsModuleWithoutApiResource(string apiResourceName)
    {
        var module = ModuleRouteFactory.Create(apiResourceName: apiResourceName);

        Assert.Throws<InvalidOperationException>(() => GatewayRoutes.Validate([module]));
    }
}
