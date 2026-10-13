using Microsoft.Extensions.Configuration;

namespace HexMaster.Battleship.Aspire.AppHost;

/// <summary>
/// Writes the Dapr configuration that makes every sidecar export its spans to the Aspire
/// dashboard. The Community Toolkit already hands each sidecar the dashboard's standard
/// <c>OTEL_EXPORTER_OTLP_*</c> settings (endpoint, protocol and the API key header), and
/// <c>daprd</c> uses them whenever its configuration names no exporter endpoint of its own.
/// Without a configuration, though, the CLI loads the <c>dapr init</c> default, which samples
/// into a local Zipkin container and assumes a TLS connection. The generated file samples every
/// request and matches transport security to the dashboard endpoint; the endpoint and the API
/// key stay in the environment, so nothing secret is written to disk.
/// </summary>
internal static class DaprTracingConfiguration
{
    // The AppHost settings that hold the dashboard's OTLP (gRPC) endpoint, newest name first.
    public static readonly string[] DashboardOtlpEndpointSettings =
    [
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL",
        "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"
    ];

    public static Uri? FindDashboardOtlpEndpoint(IConfiguration configuration)
    {
        foreach (var setting in DashboardOtlpEndpointSettings)
        {
            if (Uri.TryCreate(configuration[setting], UriKind.Absolute, out var endpoint)
                && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps))
            {
                return endpoint;
            }
        }

        return null;
    }

    public static string Content(Uri dashboardOtlpEndpoint)
    {
        var isSecure = dashboardOtlpEndpoint.Scheme == Uri.UriSchemeHttps ? "true" : "false";

        return $"""
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: battleship-tracing
            spec:
              tracing:
                samplingRate: "1"
                otel:
                  isSecure: {isSecure}

            """;
    }

    /// <summary>
    /// Writes the configuration for this run into <paramref name="directory"/> and returns its
    /// path. Returns <see langword="null"/>, after a warning, when the AppHost does not know the
    /// dashboard's OTLP endpoint; the sidecars then start with the Dapr default.
    /// </summary>
    public static string? Write(IConfiguration configuration, string directory, Action<string> warn)
    {
        var endpoint = FindDashboardOtlpEndpoint(configuration);

        if (endpoint is null)
        {
            warn(
                "Dapr sidecar tracing is not configured: none of " +
                string.Join(", ", DashboardOtlpEndpointSettings) +
                " holds the dashboard's OTLP endpoint. The sidecars start without exporting their spans to the dashboard.");
            return null;
        }

        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"dapr-tracing-{endpoint.Scheme}.yaml");
        File.WriteAllText(path, Content(endpoint));

        return path;
    }
}
