using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.DomainModels;

// A player within one game. Its setters are internal to the module so only Game changes a player;
// the game records each change in its own state.
public sealed class Player : IPlayer
{
    private Player(
        Guid playerId,
        string playerName,
        bool hasTurn,
        bool isReady,
        IReadOnlyList<ShipPlacement> ships,
        IReadOnlyList<Shot> shots,
        DateTimeOffset lastActivityOn)
    {
        if (playerId == Guid.Empty)
        {
            throw new DomainException("A player needs an id.");
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            throw new DomainException("A player needs a name.");
        }

        if (ships.DistinctBy(s => s.Kind).Count() != ships.Count)
        {
            throw new DomainException("A player places each ship kind at most once.");
        }

        PlayerId = playerId;
        PlayerName = playerName.Trim();
        HasTurn = hasTurn;
        IsReady = isReady;
        Ships = ships;
        Shots = shots;
        LastActivityOn = lastActivityOn;
    }

    public Guid PlayerId { get; }

    public string PlayerName { get; }

    public bool HasTurn { get; internal set; }

    public bool IsReady { get; internal set; }

    public IReadOnlyList<ShipPlacement> Ships { get; internal set; }

    public IReadOnlyList<Shot> Shots { get; internal set; }

    public DateTimeOffset LastActivityOn { get; internal set; }

    // A player entering a game: no turn, not ready, no ships, no shots, active now.
    internal static Player Create(Guid playerId, string playerName, DateTimeOffset now) =>
        new(playerId, playerName, false, false, [], [], now);

    public static Player Reconcile(
        Guid playerId,
        string playerName,
        bool hasTurn,
        bool isReady,
        IEnumerable<ShipPlacement> ships,
        IEnumerable<Shot> shots,
        DateTimeOffset lastActivityOn)
    {
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(shots);

        return new Player(playerId, playerName, hasTurn, isReady, [.. ships], [.. shots], lastActivityOn);
    }
}
