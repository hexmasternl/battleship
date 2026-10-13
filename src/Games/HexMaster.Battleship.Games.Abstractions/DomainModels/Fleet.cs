namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// The fleet every player places: one ship of each kind. The single source of ship lengths.
public static class Fleet
{
    public static IReadOnlyList<ShipKind> Kinds { get; } =
        [ShipKind.Carrier, ShipKind.Battleship, ShipKind.Cruiser, ShipKind.Submarine, ShipKind.Destroyer];

    public static int LengthOf(ShipKind kind) => kind switch
    {
        ShipKind.Carrier => 5,
        ShipKind.Battleship => 4,
        ShipKind.Cruiser => 3,
        ShipKind.Submarine => 3,
        ShipKind.Destroyer => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown ship kind."),
    };
}
