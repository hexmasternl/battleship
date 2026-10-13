using System.Text.Json.Serialization;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// One game as the caller sees it: the caller in full, the opponent without ships. Opponent is null
// until a second player joined. The state is written by name (New, Started, ...).
public sealed record GameView(
    Guid GameId,
    string GameCode,
    [property: JsonConverter(typeof(JsonStringEnumConverter<GameState>))] GameState State,
    PlayerView Me,
    OpponentView? Opponent);

// The caller: everything, including the placed ships.
public sealed record PlayerView(
    Guid PlayerId,
    string PlayerName,
    bool Ready,
    bool HasTurn,
    IReadOnlyList<ShipView> Ships,
    IReadOnlyList<ShotView> Shots);

// The other player. Deliberately has no ships property, so the opponent's fleet cannot leak into
// a response however the view is serialized.
public sealed record OpponentView(
    Guid PlayerId,
    string PlayerName,
    bool Ready,
    bool HasTurn,
    IReadOnlyList<ShotView> Shots);

// A placed ship by its origin cell; the frontend derives the other cells from kind and orientation.
public sealed record ShipView(ShipKind Kind, int Column, int Row, Orientation Orientation);

public sealed record ShotView(int Column, int Row, bool Hit);
