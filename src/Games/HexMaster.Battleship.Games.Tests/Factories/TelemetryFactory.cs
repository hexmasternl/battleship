using System.Diagnostics.Metrics;
using HexMaster.Battleship.Games.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.Battleship.Games.Tests.Factories;

// A GamesMetrics over a real meter factory, plus a way to read what its counters recorded.
public static class TelemetryFactory
{
    public static GamesMetrics Create(out IMeterFactory meterFactory)
    {
        meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        return new GamesMetrics(meterFactory);
    }

    // Sums every measurement of the named instrument created by the given factory.
    public static Func<long> Listen(IMeterFactory meterFactory, string instrumentName)
    {
        long total = 0;
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == instrumentName && instrument.Meter.Scope == meterFactory)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref total, value));
        listener.Start();

        return () => Interlocked.Read(ref total);
    }
}
