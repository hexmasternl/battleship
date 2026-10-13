using System.Text.Json;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Data.Entities;

// The version 1 shape of a stored game: flat ship and shot coordinates and no ready flag. Only read,
// never written; Upgrade turns it into the current shape.
public sealed record GameDocumentV1(
    Guid Id,
    string GameCode,
    GameState GameState,
    PlayerDocumentV1 PlayerOne,
    PlayerDocumentV1? PlayerTwo,
    DateTimeOffset LastActivityOn)
{
    public static GameDocumentV1 Deserialize(string json) =>
        JsonSerializer.Deserialize<GameDocumentV1>(json, GameDocument.SerializerOptions)
            ?? throw new JsonException("The game document is empty.");

    public GameDocument Upgrade() =>
        new(Id, GameCode, GameState, PlayerOne.Upgrade(), PlayerTwo?.Upgrade(), LastActivityOn);
}

public sealed record PlayerDocumentV1(
    Guid PlayerId,
    string PlayerName,
    bool HasTurn,
    IReadOnlyList<Coordinate>? ShipCoordinates,
    IReadOnlyList<Coordinate>? ShotCoordinates,
    DateTimeOffset LastActivityOn)
{
    // Nobody was ready before version 2. No version 1 code path placed ships, so loose ship cells are
    // dropped rather than guessed into placements. Shots had no hit flag and count as misses.
    public PlayerDocument Upgrade() => new(
        PlayerId,
        PlayerName,
        HasTurn,
        IsReady: false,
        Ships: [],
        Shots: [.. (ShotCoordinates ?? []).Select(c => new ShotDocument(c.Column, c.Row, Hit: false))],
        LastActivityOn);
}
