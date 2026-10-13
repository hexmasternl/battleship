namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// The body of PUT /games/{code}/ships/{kind}: the ship's origin cell and orientation. Every field is
// optional here so the endpoint can answer a missing one with a validation problem; orientation is
// "horizontal" or "vertical" in any casing.
public sealed record PlaceShipRequest(int? Column, int? Row, string? Orientation);
