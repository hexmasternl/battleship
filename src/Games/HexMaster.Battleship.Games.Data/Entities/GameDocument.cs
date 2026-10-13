using System.Text.Json;
using System.Text.Json.Serialization;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Data.Entities;

// The complete state of a game as stored in GameEntity.Document. Its shape is versioned by
// CurrentVersion, so a later change to the shape can upgrade documents written before it.
public sealed record GameDocument(
    Guid Id,
    string GameCode,
    GameState GameState,
    PlayerDocument PlayerOne,
    PlayerDocument? PlayerTwo,
    DateTimeOffset LastActivityOn)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static GameDocument From(IGame game) => new(
        game.Id,
        game.GameCode,
        game.GameState,
        PlayerDocument.From(game.PlayerOne),
        game.PlayerTwo is { } playerTwo ? PlayerDocument.From(playerTwo) : null,
        game.LastActivityOn);

    public static GameDocument Deserialize(string json, int version)
    {
        if (version != CurrentVersion)
        {
            throw new NotSupportedException($"Game document version {version} is not supported.");
        }

        return JsonSerializer.Deserialize<GameDocument>(json, SerializerOptions)
            ?? throw new JsonException("The game document is empty.");
    }

    public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

    public Game ToGame() => Game.Reconcile(
        Id,
        DomainModels.GameCode.Parse(GameCode),
        GameState,
        PlayerOne.ToPlayer(),
        PlayerTwo?.ToPlayer(),
        LastActivityOn);
}

public sealed record PlayerDocument(
    Guid PlayerId,
    string PlayerName,
    bool HasTurn,
    IReadOnlyList<Coordinate> ShipCoordinates,
    IReadOnlyList<Coordinate> ShotCoordinates,
    DateTimeOffset LastActivityOn)
{
    public static PlayerDocument From(IPlayer player) => new(
        player.PlayerId,
        player.PlayerName,
        player.HasTurn,
        player.ShipCoordinates,
        player.ShotCoordinates,
        player.LastActivityOn);

    public Player ToPlayer() =>
        Player.Reconcile(PlayerId, PlayerName, HasTurn, ShipCoordinates ?? [], ShotCoordinates ?? [], LastActivityOn);
}
