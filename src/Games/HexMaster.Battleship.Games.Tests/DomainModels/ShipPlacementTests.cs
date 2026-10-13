using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Tests.DomainModels;

public class ShipPlacementTests
{
    [Fact]
    public void Fleet_IsTheClassicFiveShips()
    {
        Assert.Equal(
            [ShipKind.Carrier, ShipKind.Battleship, ShipKind.Cruiser, ShipKind.Submarine, ShipKind.Destroyer],
            Fleet.Kinds);
        Assert.Equal([5, 4, 3, 3, 2], Fleet.Kinds.Select(Fleet.LengthOf));
    }

    [Fact]
    public void LengthOf_RefusesAnUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Fleet.LengthOf((ShipKind)99));
    }

    [Fact]
    public void Cells_RunToTheRightForAHorizontalShip()
    {
        var placement = new ShipPlacement(ShipKind.Cruiser, new Coordinate(2, 4), Orientation.Horizontal);

        Assert.Equal([new(2, 4), new(3, 4), new(4, 4)], placement.Cells);
    }

    [Fact]
    public void Cells_RunDownForAVerticalShip()
    {
        var placement = new ShipPlacement(ShipKind.Destroyer, new Coordinate(7, 10), Orientation.Vertical);

        Assert.Equal([new(7, 10), new(7, 11)], placement.Cells);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(11, 11, true)]
    [InlineData(12, 0, false)]
    [InlineData(0, 12, false)]
    [InlineData(-1, 5, false)]
    [InlineData(5, -1, false)]
    public void Board_ContainsOnlyTheTwelveByTwelveGrid(int column, int row, bool expected)
    {
        Assert.Equal(12, Board.Size);
        Assert.Equal(expected, Board.Contains(new Coordinate(column, row)));
    }
}
