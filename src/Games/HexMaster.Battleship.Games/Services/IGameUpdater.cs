using HexMaster.Battleship.Games.DomainModels;

namespace HexMaster.Battleship.Games.Services;

// Loads a game, applies a change and stores it without overwriting a change another request stored
// in between. Every handler that changes a stored game goes through it and only says what to change.
public interface IGameUpdater
{
    // Runs mutate on the freshly read game and stores the result when it asks to. On a conflict the
    // game is read again and mutate runs again on that state, so mutate must be safe to repeat. A
    // DomainException from mutate propagates and nothing is stored. Throws
    // GameUpdateConflictException when every attempt conflicts.
    Task<GameUpdateResult<T>> UpdateAsync<T>(
        GameCode code,
        Func<Game, GameMutation<T>> mutate,
        CancellationToken cancellationToken);
}
