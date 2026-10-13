using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.Games.Observability;

// The business metrics of the Games module. No tag carries a game id, game code or player id.
public sealed class GamesMetrics
{
    public const string GamesCreatedName = "games.created";

    private readonly Counter<long> _gamesCreated;

    public GamesMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(GamesTelemetry.Name);
        _gamesCreated = meter.CreateCounter<long>(GamesCreatedName, unit: "{game}", description: "Games created.");
    }

    public void GameCreated() => _gamesCreated.Add(1);
}
