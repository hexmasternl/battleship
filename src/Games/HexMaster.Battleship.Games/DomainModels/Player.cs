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
        IReadOnlyList<Coordinate> shipCoordinates,
        IReadOnlyList<Coordinate> shotCoordinates,
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

        PlayerId = playerId;
        PlayerName = playerName.Trim();
        HasTurn = hasTurn;
        ShipCoordinates = shipCoordinates;
        ShotCoordinates = shotCoordinates;
        LastActivityOn = lastActivityOn;
    }

    public Guid PlayerId { get; }

    public string PlayerName { get; }

    public bool HasTurn { get; internal set; }

    public IReadOnlyList<Coordinate> ShipCoordinates { get; }

    public IReadOnlyList<Coordinate> ShotCoordinates { get; }

    public DateTimeOffset LastActivityOn { get; internal set; }

    // A player entering a game: no turn, no ships, no shots, active now.
    internal static Player Create(Guid playerId, string playerName, DateTimeOffset now) =>
        new(playerId, playerName, false, [], [], now);

    public static Player Reconcile(
        Guid playerId,
        string playerName,
        bool hasTurn,
        IEnumerable<Coordinate> shipCoordinates,
        IEnumerable<Coordinate> shotCoordinates,
        DateTimeOffset lastActivityOn)
    {
        ArgumentNullException.ThrowIfNull(shipCoordinates);
        ArgumentNullException.ThrowIfNull(shotCoordinates);

        return new Player(playerId, playerName, hasTurn, [.. shipCoordinates], [.. shotCoordinates], lastActivityOn);
    }
}
