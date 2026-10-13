using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.Games.Observability;

// The business metrics of the Games module. No tag carries a game id, game code or player id.
public sealed class GamesMetrics
{
    public const string GamesCreatedName = "games.created";
    public const string GamesJoinedName = "games.joined";
    public const string UpdateConflictsName = "games.update.conflicts";
    public const string PlayersReadyName = "games.player-ready";
    public const string GamesStartedName = "games.started";
    public const string ShotsFiredName = "games.shots.fired";
    public const string HitTag = "hit";

    private readonly Counter<long> _gamesCreated;
    private readonly Counter<long> _shotsFired;
    private readonly Counter<long> _gamesJoined;
    private readonly Counter<long> _updateConflicts;
    private readonly Counter<long> _playersReady;
    private readonly Counter<long> _gamesStarted;

    public GamesMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(GamesTelemetry.Name);
        _gamesCreated = meter.CreateCounter<long>(GamesCreatedName, unit: "{game}", description: "Games created.");
        _gamesJoined = meter.CreateCounter<long>(GamesJoinedName, unit: "{game}", description: "Games joined by a second player.");
        _updateConflicts = meter.CreateCounter<long>(
            UpdateConflictsName, unit: "{conflict}", description: "Game updates that found the game changed by another request.");
        _playersReady = meter.CreateCounter<long>(
            PlayersReadyName, unit: "{player}", description: "Players who placed their fleet and became ready.");
        _gamesStarted = meter.CreateCounter<long>(GamesStartedName, unit: "{game}", description: "Games started by two ready players.");
        _shotsFired = meter.CreateCounter<long>(ShotsFiredName, unit: "{shot}", description: "Shots recorded, tagged hit or miss.");
    }

    public void ShotFired(bool hit) => _shotsFired.Add(1, new KeyValuePair<string, object?>(HitTag, hit));

    public void PlayerReady() => _playersReady.Add(1);

    public void GameStarted() => _gamesStarted.Add(1);

    public void GameCreated() => _gamesCreated.Add(1);

    public void GameJoined() => _gamesJoined.Add(1);

    public void UpdateConflicted() => _updateConflicts.Add(1);
}
