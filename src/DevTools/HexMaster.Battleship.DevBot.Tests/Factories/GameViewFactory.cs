using Bogus;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Tests.Factories;

internal static class GameViewFactory
{
    private static readonly Faker Faker = new();

    public static readonly Guid BotId = Guid.Parse("b0b0b0b0-0000-0000-0000-000000000001");

    public static string Code() => Faker.Random.String2(8, "ABCDEFGHJKMNPQRSTUVWXYZ23456789");

    public static GameView View(
        string code,
        GameState state = GameState.New,
        IReadOnlyList<ShipView>? ships = null,
        bool ready = false,
        bool hasTurn = false,
        IReadOnlyList<ShotView>? shots = null,
        Guid? winnerId = null) =>
        new(
            Guid.NewGuid(),
            code,
            state,
            new PlayerView(BotId, "Dev Bot", ready, hasTurn, ships ?? [], shots ?? []),
            new OpponentView(Guid.NewGuid(), Faker.Name.FirstName(), false, false, []),
            winnerId);

    public static IReadOnlyList<ShipView> FullFleet() =>
    [
        .. Fleet.Kinds.Select((kind, row) => new ShipView(kind, 0, row, Orientation.Horizontal)),
    ];
}
