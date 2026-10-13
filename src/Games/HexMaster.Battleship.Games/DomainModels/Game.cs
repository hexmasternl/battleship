using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Games.Abstractions.DomainModels;
using HexMaster.Battleship.Games.Exceptions;
using HexMaster.Battleship.Games.Services;

namespace HexMaster.Battleship.Games.DomainModels;

// The game aggregate. It owns the turn rule: at most one player has the turn, nobody has it while
// the game is New, and a switch flips both players in one step. Every mutation takes the current
// instant and ends by recording activity on the game and, when a player is involved, that player.
public sealed class Game : DomainModel<Guid>, IGame
{
    private readonly Player _playerOne;
    private Player? _playerTwo;

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

    // Seats the caller as player two of a new game. A player of the game always gets back in, in any
    // state, without a change; anyone else is refused once the game started or the seat is taken.
    public JoinOutcome Join(Guid playerId, string playerName, DateTimeOffset now)
    {
        if (IsPlayer(playerId))
        {
            return JoinOutcome.AlreadyPlayer;
        }

        if (GameState != GameState.New)
        {
            throw new GameJoinRefusedException(GameCode, GameJoinRefusedReason.NotJoinable);
        }

        if (_playerTwo is not null)
        {
            throw new GameJoinRefusedException(GameCode, GameJoinRefusedReason.GameFull);
        }

        var player = Player.Create(playerId, playerName, now);
        Change(_playerTwo, player, v => _playerTwo = v);
        Touch(now, player);

        return JoinOutcome.Joined;
    }

    public bool IsPlayer(Guid playerId) =>
        _playerOne.PlayerId == playerId || _playerTwo?.PlayerId == playerId;

    // Places one of the player's ships, or moves it when that kind is already placed. Ships lie fully
    // on the board and may touch, but never share a cell with another ship of the same player.
    public void PlaceShip(Guid playerId, ShipPlacement placement, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var player = PlayerWithId(playerId);

        if (GameState != GameState.New)
        {
            throw new PlacementRefusedException(GameCode, PlacementRefusedReason.GameAlreadyStarted,
                $"Ships can only be placed in a new game; game {GameCode} is {GameState}.");
        }

        if (player.IsReady)
        {
            throw new PlacementRefusedException(GameCode, PlacementRefusedReason.PlayerReady,
                "The fleet of a ready player cannot change.");
        }

        var cells = placement.Cells;
        if (!cells.All(Board.Contains))
        {
            throw new PlacementRefusedException(GameCode, PlacementRefusedReason.OutOfBoard,
                $"The {placement.Kind} does not fit on the board there.");
        }

        var others = player.Ships.Where(s => s.Kind != placement.Kind).ToList();
        if (others.FirstOrDefault(s => s.Cells.Intersect(cells).Any()) is { } overlapped)
        {
            throw new PlacementRefusedException(GameCode, PlacementRefusedReason.Overlap,
                $"The {placement.Kind} would overlap the {overlapped.Kind}.");
        }

        IReadOnlyList<ShipPlacement> ships = [.. others, placement];
        Change(player.Ships, ships, v => player.Ships = v);
        Touch(now, player);
    }

    // Marks the player ready once the whole fleet is placed. When both players are ready the game
    // starts and the picker chooses who shoots first. Repeating it is harmless in any state.
    public ReadyOutcome MarkReady(Guid playerId, IFirstTurnPicker picker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(picker);
        var player = PlayerWithId(playerId);

        if (player.IsReady)
        {
            return ReadyOutcome.AlreadyReady;
        }

        if (GameState != GameState.New)
        {
            throw new PlacementRefusedException(GameCode, PlacementRefusedReason.GameAlreadyStarted,
                $"Only a player of a new game can become ready; game {GameCode} is {GameState}.");
        }

        if (Fleet.Kinds.Except(player.Ships.Select(s => s.Kind)).Any())
        {
            throw new PlacementRefusedException(GameCode, PlacementRefusedReason.FleetIncomplete,
                "Place every ship before getting ready.");
        }

        Change(player.IsReady, true, v => player.IsReady = v);
        Touch(now, player);

        if (_playerTwo is null || !_playerOne.IsReady || !_playerTwo.IsReady)
        {
            return ReadyOutcome.Ready;
        }

        Start(picker, now);
        return ReadyOutcome.ReadyAndStarted;
    }

    // Fires the player's shot at the opponent's board. Refused, without any change, when the game is
    // not started, the player does not have the turn or already fired at that cell. A recorded shot,
    // hit or miss, always passes the turn to the opponent.
    public ShotOutcome FireShot(Guid playerId, Coordinate at, DateTimeOffset now)
    {
        var shooter = PlayerWithId(playerId);

        if (GameState != GameState.Started)
        {
            throw new ShotRefusedException(GameCode, ShotRefusedReason.GameNotStarted,
                $"Shots can only be fired in a started game; game {GameCode} is {GameState}.");
        }

        if (!shooter.HasTurn)
        {
            throw new ShotRefusedException(GameCode, ShotRefusedReason.NotYourTurn,
                "The other player has the turn.");
        }

        // The endpoint refuses an off-board cell first; this only guards other callers.
        if (!Board.Contains(at))
        {
            throw new DomainException($"Column {at.Column}, row {at.Row} is not on the board.");
        }

        if (shooter.Shots.Any(s => s.Column == at.Column && s.Row == at.Row))
        {
            throw new ShotRefusedException(GameCode, ShotRefusedReason.CellAlreadyShot,
                $"You already fired at column {at.Column}, row {at.Row}.");
        }

        var opponent = shooter == _playerOne ? _playerTwo! : _playerOne;
        var hit = opponent.Ships.SelectMany(s => s.Cells).Contains(at);

        IReadOnlyList<Shot> shots = [.. shooter.Shots, new Shot(at.Column, at.Row, hit)];
        Change(shooter.Shots, shots, v => shooter.Shots = v);
        SwitchTurn(now);
        Touch(now, shooter);

        return new ShotOutcome(at, hit);
    }

    private void Start(IFirstTurnPicker picker, DateTimeOffset now)
    {
        Change(GameState, GameState.Started, v => GameState = v);
        GiveFirstTurnTo(picker.Pick(_playerOne.PlayerId, _playerTwo!.PlayerId), now);
    }

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
