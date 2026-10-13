using System.Diagnostics.CodeAnalysis;

namespace HexMaster.Battleship.DevBot.Client;

// What a games API call came to, keyed on the problem type slugs the Games API publishes.
public enum GameCallStatus
{
    Ok,
    NotFound,
    GameFull,
    GameAlreadyStarted,
    GameNotStarted,
    NotYourTurn,
    CellAlreadyShot,
    PlayerReady,
    FleetIncomplete,
    PlacementRefused,
    UpdateConflict,
    Unauthorized,
    Failed,
}

public sealed record GameCallResult<T>(GameCallStatus Status, T? Value, string? Detail = null)
    where T : class
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsOk => Status == GameCallStatus.Ok && Value is not null;

    public static GameCallResult<T> Ok(T value) => new(GameCallStatus.Ok, value);

    public static GameCallResult<T> Problem(GameCallStatus status, string? detail) => new(status, null, detail);
}
