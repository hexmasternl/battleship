namespace HexMaster.Battleship.Games.Services;

// What a mutation passed to IGameUpdater did to the game: changed it, so it must be stored, or left
// it as it was. Either way it carries the value the caller needs afterwards.
public readonly record struct GameMutation<T>(T Value, bool HasChanges);

public static class GameMutation
{
    public static GameMutation<T> Save<T>(T value) => new(value, HasChanges: true);

    public static GameMutation<T> NoChange<T>(T value) => new(value, HasChanges: false);
}
