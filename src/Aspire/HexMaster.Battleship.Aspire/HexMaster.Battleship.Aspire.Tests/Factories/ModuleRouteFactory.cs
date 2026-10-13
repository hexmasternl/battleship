using Bogus;
using HexMaster.Battleship.Aspire.AppHost;

namespace HexMaster.Battleship.Aspire.Tests.Factories;

/// <summary>Builds valid <see cref="ModuleRoute"/> instances with plausible defaults.</summary>
internal static class ModuleRouteFactory
{
    private static readonly Faker Faker = new();

    public static ModuleRoute Create(
        string? module = null,
        string? apiResourceName = null,
        IReadOnlyList<string>? prefixes = null)
    {
        var name = module ?? Prefix();

        return new ModuleRoute(
            name,
            apiResourceName ?? $"hexmaster-battleship-{name}-api",
            prefixes ?? [name]);
    }

    // A lowercase, letters-only segment that cannot collide with a reserved prefix.
    public static string Prefix() => $"m{Faker.Random.String2(8, "abcdefghijklmnopqrstuvwxyz")}";
}
