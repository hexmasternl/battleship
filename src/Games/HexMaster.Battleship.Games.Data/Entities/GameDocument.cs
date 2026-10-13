using System.Text.Json;
using System.Text.Json.Serialization;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Data.Entities;

// The complete state of a game as stored in GameEntity.Document. Its shape is versioned by
// CurrentVersion, so a later change to the shape can upgrade documents written before it.
// Version 2 holds ship placements, shots with a hit flag and the ready flag; version 1 documents
// (GameDocumentV1) are upgraded when read and written back as version 2 on their next update.
// WinnerId is an additive, optional field of version 2: a document written before it reads as a game
// without a winner, which every New or Started game is anyway.
public sealed record GameDocument(
    Guid Id,
    string GameCode,
    GameState GameState,
    PlayerDocument PlayerOne,
    PlayerDocument? PlayerTwo,
    DateTimeOffset LastActivityOn,
    Guid? WinnerId = null)
{
    public const int CurrentVersion = 2;

    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static GameDocument From(IGame game) => new(
        game.Id,
        game.GameCode,
        game.GameState,
        PlayerDocument.From(game.PlayerOne),
        game.PlayerTwo is { } playerTwo ? PlayerDocument.From(playerTwo) : null,
        game.LastActivityOn,
        game.WinnerId);

    public static GameDocument Deserialize(string json, int version) => version switch
    {
        1 => GameDocumentV1.Deserialize(json).Upgrade(),
        CurrentVersion => JsonSerializer.Deserialize<GameDocument>(json, SerializerOptions)
            ?? throw new JsonException("The game document is empty."),
        _ => throw new NotSupportedException($"Game document version {version} is not supported."),
    };

    public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

    public Game ToGame() => Game.Reconcile(
        Id,
        DomainModels.GameCode.Parse(GameCode),
        GameState,
        PlayerOne.ToPlayer(),
        PlayerTwo?.ToPlayer(),
        LastActivityOn,
        WinnerId);
}

public sealed record PlayerDocument(
    Guid PlayerId,
    string PlayerName,
    bool HasTurn,
    bool IsReady,
    IReadOnlyList<ShipDocument> Ships,
    IReadOnlyList<ShotDocument> Shots,
    DateTimeOffset LastActivityOn)
{
    public static PlayerDocument From(IPlayer player) => new(
        player.PlayerId,
        player.PlayerName,
        player.HasTurn,
        player.IsReady,
        [.. player.Ships.Select(ShipDocument.From)],
        [.. player.Shots.Select(ShotDocument.From)],
        player.LastActivityOn);

    public Player ToPlayer() => Player.Reconcile(
        PlayerId,
        PlayerName,
        HasTurn,
        IsReady,
        (Ships ?? []).Select(s => s.ToPlacement()),
        (Shots ?? []).Select(s => s.ToShot()),
        LastActivityOn);
}

// A placed ship: its kind, origin cell and orientation. Its cells are derived, never stored.
public sealed record ShipDocument(ShipKind Kind, int Column, int Row, Orientation Orientation)
{
    public static ShipDocument From(ShipPlacement placement) =>
        new(placement.Kind, placement.Origin.Column, placement.Origin.Row, placement.Orientation);

    public ShipPlacement ToPlacement() => new(Kind, new Coordinate(Column, Row), Orientation);
}

public sealed record ShotDocument(int Column, int Row, bool Hit)
{
    public static ShotDocument From(Shot shot) => new(shot.Column, shot.Row, shot.Hit);

    public Shot ToShot() => new(Column, Row, Hit);
}
