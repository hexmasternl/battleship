using Bogus;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Services;

namespace HexMaster.Battleship.Games.Tests.Factories;

// Builds plausible games and players; tests override only the parts they assert on.
public static class GameFactory
{
    private static readonly Faker Faker = new();
    private static readonly GameCodeGenerator Codes = new();

    public static string PlayerName() => $"{Faker.PickRandom("Admiral", "Captain", "Commodore")} {Faker.Name.LastName()}";

    public static GameCode Code() => Codes.Generate();

    public static Coordinate Coordinate() => new(Faker.Random.Int(0, 11), Faker.Random.Int(0, 11));

    public static Player Player(
        Guid? playerId = null,
        string? playerName = null,
        bool hasTurn = false,
        IEnumerable<Coordinate>? ships = null,
        IEnumerable<Coordinate>? shots = null,
        DateTimeOffset? lastActivityOn = null) =>
        Games.DomainModels.Player.Reconcile(
            playerId ?? Guid.NewGuid(),
            playerName ?? PlayerName(),
            hasTurn,
            ships ?? [],
            shots ?? [],
            lastActivityOn ?? Faker.Date.PastOffset());

    // A stored game of two players; by default started and with nobody holding the turn.
    public static Game Stored(
        GameState gameState = GameState.Started,
        Player? playerOne = null,
        Player? playerTwo = null,
        bool withoutPlayerTwo = false,
        DateTimeOffset? lastActivityOn = null) =>
        Game.Reconcile(
            Guid.NewGuid(),
            Code(),
            gameState,
            playerOne ?? Player(),
            withoutPlayerTwo ? null : playerTwo ?? Player(),
            lastActivityOn ?? Faker.Date.PastOffset());
}
