using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.Games.Observability;

// The business metrics of the Games module. No tag carries a game id, game code or player id.
public sealed class GamesMetrics
{
    public const string GamesCreatedName = "games.created";
    public const string GamesJoinedName = "games.joined";
    public const string UpdateConflictsName = "games.update.conflicts";

    private readonly Counter<long> _gamesCreated;
    private readonly Counter<long> _gamesJoined;
    private readonly Counter<long> _updateConflicts;

    public GamesMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(GamesTelemetry.Name);
        _gamesCreated = meter.CreateCounter<long>(GamesCreatedName, unit: "{game}", description: "Games created.");
        _gamesJoined = meter.CreateCounter<long>(GamesJoinedName, unit: "{game}", description: "Games joined by a second player.");
        _updateConflicts = meter.CreateCounter<long>(
            UpdateConflictsName, unit: "{conflict}", description: "Game updates that found the game changed by another request.");
    }

    public void GameCreated() => _gamesCreated.Add(1);

    public void GameJoined() => _gamesJoined.Add(1);

    public void UpdateConflicted() => _updateConflicts.Add(1);
}
