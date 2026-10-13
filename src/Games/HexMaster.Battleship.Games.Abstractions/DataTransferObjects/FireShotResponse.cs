using System.Text.Json.Serialization;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

// The answer to a recorded shot: the cell fired at, whether it hit a ship and the state of the game
// after the shot, written by name as in GameView. Completed tells the shooter that their shot won.
public sealed record FireShotResponse(
    int Column,
    int Row,
    bool Hit,
    [property: JsonConverter(typeof(JsonStringEnumConverter<GameState>))] GameState State);
