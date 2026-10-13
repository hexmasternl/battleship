using HexMaster.Battleship.Aspire.AppHost;
using Microsoft.Extensions.Configuration;

namespace HexMaster.Battleship.Aspire.Tests;

public sealed class DaprTracingConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "battleship-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("http://localhost:19096", "false")]
    [InlineData("https://localhost:21001", "true")]
    public void Write_SamplesEverythingAndMatchesTransportSecurityToTheDashboard(string endpoint, string isSecure)
    {
        var warnings = new List<string>();

        var path = DaprTracingConfiguration.Write(
            Configuration(("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", endpoint)), _directory, warnings.Add);

        Assert.NotNull(path);
        Assert.Empty(warnings);
        var content = File.ReadAllText(path);
        Assert.Contains("kind: Configuration", content);
        Assert.Contains("samplingRate: \"1\"", content);
        Assert.Contains($"isSecure: {isSecure}", content);
        // Endpoint and API key reach the sidecar through its OTEL_* environment, never through the file.
        Assert.DoesNotContain("endpointAddress", content);
        Assert.DoesNotContain("zipkin", content);
    }

    [Fact]
    public void Write_FallsBackToTheLegacyDashboardSetting()
    {
        var path = DaprTracingConfiguration.Write(
            Configuration(("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:4317")), _directory, _ => { });

        Assert.NotNull(path);
        Assert.Contains("isSecure: false", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    public void Write_WithoutADashboardEndpoint_WarnsAndWritesNothing(string? endpoint)
    {
        var warnings = new List<string>();

        var path = DaprTracingConfiguration.Write(
            Configuration(("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", endpoint)), _directory, warnings.Add);

        Assert.Null(path);
        Assert.Contains("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", Assert.Single(warnings));
        Assert.False(Directory.Exists(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();
}
