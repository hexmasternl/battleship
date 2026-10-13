using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.Games.DomainModels;

// The game aggregate. It owns the turn rule: at most one player has the turn, nobody has it while
// the game is New, and a switch flips both players in one step. Every mutation takes the current
// instant and ends by recording activity on the game and, when a player is involved, that player.
public sealed class Game : DomainModel<Guid>, IGame
{
    private readonly Player _playerOne;
    private readonly Player? _playerTwo;

    private Game(
        Guid id,
        GameCode gameCode,
        GameState gameState,
        Player playerOne,
        Player? playerTwo,
        DateTimeOffset lastActivityOn,
        DomainModelState state)
        : base(id, state)
    {
        ArgumentNullException.ThrowIfNull(playerOne);
        if (string.IsNullOrEmpty(gameCode.Value))
        {
            throw new DomainException("A game needs a game code.");
        }

        _playerOne = playerOne;
        _playerTwo = playerTwo;
        Code = gameCode;
        GameState = gameState;
        LastActivityOn = lastActivityOn;

        EnsureTurnRule();
    }

    public GameCode Code { get; }

    public string GameCode => Code.Value;

    public GameState GameState { get; private set; }

    public IPlayer PlayerOne => _playerOne;

    public IPlayer? PlayerTwo => _playerTwo;

    public DateTimeOffset LastActivityOn { get; private set; }

    // A new game waiting for a second player, created by player one.
    public static Game Create(Guid playerId, string playerName, GameCode gameCode, DateTimeOffset now) =>
        new(Guid.NewGuid(), gameCode, GameState.New, Player.Create(playerId, playerName, now), null, now, DomainModelState.New);

    // Restores a stored game. A stored state that breaks the turn rule is refused.
    public static Game Reconcile(
        Guid id,
        GameCode gameCode,
        GameState gameState,
        Player playerOne,
        Player? playerTwo,
        DateTimeOffset lastActivityOn) =>
        new(id, gameCode, gameState, playerOne, playerTwo, lastActivityOn, DomainModelState.Pristine);

    public void GiveFirstTurnTo(Guid playerId, DateTimeOffset now)
    {
        EnsureStarted();
        var player = PlayerWithId(playerId);
        if (_playerOne.HasTurn || _playerTwo is { HasTurn: true })
        {
            throw new DomainException("A player already has the turn; switch the turn instead.");
        }

        Change(player.HasTurn, true, v => player.HasTurn = v);
        Touch(now, player);
    }

    public void SwitchTurn(DateTimeOffset now)
    {
        EnsureStarted();
        if (_playerTwo is null)
        {
            throw new DomainException("The turn can only be switched between two players.");
        }

        var (current, next) = _playerOne.HasTurn ? (_playerOne, _playerTwo) : (_playerTwo, _playerOne);
        if (!current.HasTurn)
        {
            throw new DomainException("Nobody has the turn yet; give the first turn instead.");
        }

        // Both flags change inside one assignment, so no observer sees both or neither set.
        Change(current.HasTurn, false, _ =>
        {
            current.HasTurn = false;
            next.HasTurn = true;
        });
        Touch(now, current);
    }

    // Records activity on the game and, when given, on the player who acted or was acted for.
    private void Touch(DateTimeOffset now, Player? player = null)
    {
        Change(LastActivityOn, now, v => LastActivityOn = v);
        if (player is not null)
        {
            Change(player.LastActivityOn, now, v => player.LastActivityOn = v);
        }
    }

    private Player PlayerWithId(Guid playerId) =>
        _playerOne.PlayerId == playerId ? _playerOne
        : _playerTwo?.PlayerId == playerId ? _playerTwo
        : throw new DomainException($"Player {playerId} does not play in game {GameCode}.");

    private void EnsureStarted()
    {
        if (GameState != GameState.Started)
        {
            throw new DomainException($"The turn only changes in a started game; game {GameCode} is {GameState}.");
        }
    }

    private void EnsureTurnRule()
    {
        if (_playerOne.HasTurn && _playerTwo is { HasTurn: true })
        {
            throw new DomainException("Only one player can have the turn.");
        }

        if (GameState == GameState.New && (_playerOne.HasTurn || _playerTwo is { HasTurn: true }))
        {
            throw new DomainException("Nobody has the turn in a new game.");
        }
    }
}
