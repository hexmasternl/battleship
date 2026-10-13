using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.Games.Features.FireShot;

// The shot that was recorded, or NotFound when no game has the code or the caller does not play in it.
public sealed record FireShotResult(FireShotResponse? Shot)
{
    public static FireShotResult NotFound { get; } = new((FireShotResponse?)null);

    public static FireShotResult Success(FireShotResponse shot) => new(shot);
}
