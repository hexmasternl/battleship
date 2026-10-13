namespace HexMaster.Battleship.Games.Abstractions.DomainModels;

// Where one ship lies: its kind, its top or left cell and its orientation. The cells it covers are
// derived, never stored, so a stored placement cannot disagree with its own cells.
public sealed record ShipPlacement(ShipKind Kind, Coordinate Origin, Orientation Orientation)
{
    public IReadOnlyList<Coordinate> Cells
    {
        get
        {
            var length = Fleet.LengthOf(Kind);
            var cells = new Coordinate[length];
            for (var i = 0; i < length; i++)
            {
                cells[i] = Orientation == Orientation.Horizontal
                    ? Origin with { Column = Origin.Column + i }
                    : Origin with { Row = Origin.Row + i };
            }

            return cells;
        }
    }
}
