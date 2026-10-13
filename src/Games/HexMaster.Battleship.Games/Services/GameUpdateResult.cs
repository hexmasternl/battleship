namespace HexMaster.Battleship.Games.Services;

// The result of IGameUpdater.UpdateAsync: the game was not found, or the mutation ran on the stored
// game and returned Value. Saved tells whether a change was stored.
public readonly record struct GameUpdateResult<T>
{
    private GameUpdateResult(bool found, T? value, bool saved)
    {
        Found = found;
        Value = value;
        Saved = saved;
    }

    public static GameUpdateResult<T> NotFound { get; } = new(false, default, false);

    public bool Found { get; }

    public T? Value { get; }

    public bool Saved { get; }

    public static GameUpdateResult<T> Completed(T value, bool saved) => new(true, value, saved);
}
