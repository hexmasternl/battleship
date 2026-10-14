using System.Text.Json;
using HexMaster.Battleship.Aspire.AppHost;

namespace HexMaster.Battleship.Aspire.Tests;

/// <summary>
/// The Azure deployment configures its gateway from <c>infra/gateway-routes.json</c>. These tests
/// keep that file and <see cref="GatewayRoutes.Modules"/> in step, so a prefix added, renamed or
/// removed on one side only fails the build instead of drifting between local and production.
/// </summary>
public sealed class GatewayRoutesFileTests
{
    private const string ApiResourcePrefix = "hexmaster-battleship-";

    [Fact]
    public void RoutesFile_ListsExactlyTheModulesOfTheRouteTable()
    {
        var file = ReadRoutesFile();

        Assert.Equal(
            GatewayRoutes.Modules.Select(m => m.Module).Order(),
            file.Select(m => m.Module).Order());
    }

    [Fact]
    public void RoutesFile_DeclaresTheSamePrefixesPerModule()
    {
        var file = ReadRoutesFile().ToDictionary(m => m.Module);

        foreach (var module in GatewayRoutes.Modules)
        {
            Assert.True(file.ContainsKey(module.Module), $"Module '{module.Module}' is missing from infra/gateway-routes.json.");
            Assert.Equal(module.Prefixes.Order(), file[module.Module].Prefixes.Order());
        }
    }

    [Fact]
    public void RoutesFile_RoutesEachModuleToTheContainerAppOfItsApi()
    {
        var file = ReadRoutesFile().ToDictionary(m => m.Module);

        foreach (var module in GatewayRoutes.Modules)
        {
            Assert.StartsWith(ApiResourcePrefix, module.ApiResourceName, StringComparison.Ordinal);
            Assert.Equal(module.ApiResourceName[ApiResourcePrefix.Length..], file[module.Module].App);
        }
    }

    [Fact]
    public void RoutesFile_PassesTheRouteTableValidation()
    {
        var asModules = ReadRoutesFile()
            .Select(m => new ModuleRoute(m.Module, m.App, m.Prefixes))
            .ToList();

        GatewayRoutes.Validate(asModules);
    }

    private static IReadOnlyList<RoutesFileModule> ReadRoutesFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "gateway-routes.json");
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<RoutesFile>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(file);
        return file.Modules;
    }

    private sealed record RoutesFile(IReadOnlyList<RoutesFileModule> Modules);

    private sealed record RoutesFileModule(string Module, string App, IReadOnlyList<string> Prefixes);
}
