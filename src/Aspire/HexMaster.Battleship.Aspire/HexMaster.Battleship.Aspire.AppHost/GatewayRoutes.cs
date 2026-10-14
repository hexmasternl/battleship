namespace HexMaster.Battleship.Aspire.AppHost;

/// <summary>
/// One module behind the gateway: the API resource that serves it and every public path prefix
/// it owns. Each prefix becomes a <c>/{prefix}/{**catch-all}</c> route to that API, with the
/// path passed through unchanged.
/// </summary>
internal sealed record ModuleRoute(string Module, string ApiResourceName, IReadOnlyList<string> Prefixes);

/// <summary>
/// The gateway route table. This is the only place that decides which prefix reaches which
/// module API; it is owned by the <c>yarp-gateway-routes</c> skill, which proposes every new,
/// renamed or removed prefix for approval before it lands here.
/// </summary>
internal static class GatewayRoutes
{
    public const string UsersApi = "hexmaster-battleship-users-api";
    public const string GamesApi = "hexmaster-battleship-games-api";

    // First path segments the module APIs use for platform, diagnostics and scheduled maintenance
    // endpoints (the Games cleanup a Dapr cron binding posts to /maintenance). They stay
    // reachable on each API's own port and must never be routed through the gateway.
    private static readonly HashSet<string> ReservedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "alive",
        "dapr",
        "health",
        "integration-events",
        "maintenance",
        "openapi",
        "scalar",
        "smoke",
        "weatherforecast",
    };

    public static IReadOnlyList<ModuleRoute> Modules { get; } =
    [
        new("users", UsersApi, ["users"]),
        new("games", GamesApi, ["games"]),
    ];

    /// <summary>The YARP route path for a prefix.</summary>
    public static string RoutePath(string prefix) => $"/{prefix}/{{**catch-all}}";

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> naming the offending module and prefix
    /// when a route is empty, not a single lowercase path segment, reserved, or declared twice.
    /// </summary>
    public static void Validate(IEnumerable<ModuleRoute> modules)
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            if (string.IsNullOrWhiteSpace(module.ApiResourceName))
            {
                throw new InvalidOperationException(
                    $"Gateway route for module '{module.Module}' has no API resource name.");
            }

            if (module.Prefixes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Gateway route for module '{module.Module}' declares no prefixes; list each prefix explicitly.");
            }

            foreach (var prefix in module.Prefixes)
            {
                if (!IsSinglePathSegment(prefix))
                {
                    throw new InvalidOperationException(
                        $"Gateway prefix '{prefix}' of module '{module.Module}' must be one lowercase path segment (a-z, 0-9, '-').");
                }

                if (ReservedPrefixes.Contains(prefix))
                {
                    throw new InvalidOperationException(
                        $"Gateway prefix '{prefix}' of module '{module.Module}' is reserved for internal endpoints and must not be routed.");
                }

                if (!owners.TryAdd(prefix, module.Module))
                {
                    throw new InvalidOperationException(
                        $"Gateway prefix '{prefix}' is declared by both '{owners[prefix]}' and '{module.Module}'.");
                }
            }
        }
    }

    private static bool IsSinglePathSegment(string prefix) =>
        !string.IsNullOrEmpty(prefix)
        && prefix[0] != '-'
        && prefix.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
}
