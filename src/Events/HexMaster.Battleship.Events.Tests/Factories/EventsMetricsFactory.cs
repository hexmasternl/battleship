using System.Diagnostics.Metrics;
using HexMaster.Battleship.Events.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.Battleship.Events.Tests.Factories;

// EventsMetrics over its own meter factory, so a test's MetricCollector sees only its own measurements.
public static class EventsMetricsFactory
{
    public static EventsMetrics Create() => Create(out _);

    public static EventsMetrics Create(out IMeterFactory meterFactory)
    {
        meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        return new EventsMetrics(meterFactory);
    }
}
