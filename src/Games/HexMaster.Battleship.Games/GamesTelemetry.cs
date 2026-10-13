using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.Games;

// The traces and metrics of the Games module. The API registers Name as both a tracing source and
// a meter, so activities and counters started here reach the configured exporters.
public sealed class GamesTelemetry
{
    public const string Name = "HexMaster.Battleship.Games";

    public static readonly ActivitySource ActivitySource = new(Name);

    private readonly Counter<long> _gamesCreated;

    public GamesTelemetry(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(Name);
        _gamesCreated = meter.CreateCounter<long>("games.created", unit: "{game}", description: "Games created.");
    }

    public void GameCreated() => _gamesCreated.Add(1);
}
