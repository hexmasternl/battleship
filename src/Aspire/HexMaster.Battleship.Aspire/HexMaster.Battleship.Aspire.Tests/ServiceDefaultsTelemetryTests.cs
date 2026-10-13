using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace HexMaster.Battleship.Aspire.Tests;

public sealed class ServiceDefaultsTelemetryTests
{
    [Fact]
    public void AddServiceDefaults_RegistersTheTracingAndMetricsPipelines()
    {
        using var host = BuildHost();

        Assert.NotNull(host.Services.GetService<TracerProvider>());
        Assert.NotNull(host.Services.GetService<MeterProvider>());
    }

    [Fact]
    public void AddServiceDefaults_SamplesEveryBattleshipActivitySource()
    {
        using var host = BuildHost();
        _ = host.Services.GetRequiredService<TracerProvider>();

        using var battleship = new ActivitySource("HexMaster.Battleship.Test");
        using var other = new ActivitySource("Some.Other.Library");

        using var sampled = battleship.StartActivity("probe");
        using var ignored = other.StartActivity("probe");

        Assert.NotNull(sampled);
        Assert.True(sampled.Recorded);
        Assert.Null(ignored);
    }

    [Fact]
    public void AddServiceDefaults_KeepsParsedStateValuesOnLogRecords()
    {
        using var host = BuildHost();

        var options = host.Services.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

        Assert.True(options.ParseStateValues);
        Assert.True(options.IncludeFormattedMessage);
        Assert.True(options.IncludeScopes);
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        return builder.Build();
    }
}
