import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { filter, pairwise } from 'rxjs';
import { EventsService } from '../../events/events.service';
import { RealtimeEvent } from '../../events/realtime-event';
import { RealtimeEventType } from '../../events/realtime-event-types';
import { GameApiError, gameApiErrorMessage, isGameApiError } from '../game-api-error';
import { GameView, Orientation, ShipKind, ShipView, ShotView } from '../game-view';
import { FireShotError, FireShotFailure, GamesApi, isFireShotConflict } from '../games-api';
import {
  Cell,
  FLEET,
  cellLabel,
  cellsOf,
  checkPlacement,
  nameOf,
  refusalMessage,
  rotated,
  withPlacement,
} from '../placement/placement-rules';

const NO_SHIPS: readonly ShipView[] = [];
const NO_SHOTS: readonly ShotView[] = [];

/** Where the page is with loading its game. */
export type GamePageStatus = 'loading' | 'loaded' | 'not-found' | 'failed';

/** A ship being dragged: which, how it lies, which of its cells is under the pointer and where. */
export interface ShipDrag {
  readonly kind: ShipKind;
  readonly orientation: Orientation;
  /** The index of the grabbed cell along the ship, so that cell stays under the pointer. */
  readonly grabIndex: number;
  /** The origin the ship would get if dropped now; `null` while the pointer is off the board. */
  readonly origin: Cell | null;
}

/**
 * Where the caller is with their shot: free to fire, a shot request in flight, or a shot recorded
 * and the turn not yet moved on (only a turn change or a reload frees the next shot).
 */
export type ShotStatus = 'idle' | 'firing' | 'fired';

/** A shot as the `game.shot-fired.v1` event reports it: who fired, where, and whether it hit. */
export interface FiredShot extends ShotView {
  readonly playerId: string;
}

export interface GamePageState {
  readonly code: string;
  readonly status: GamePageStatus;
  /** The caller's view as the server last answered it, with local changes applied on top. */
  readonly game: GameView | null;
  /** The ship selected for tap, keyboard placement and rotation. At most one. */
  readonly activeKind: ShipKind | null;
  /** The ship being dragged, if any. */
  readonly drag: ShipDrag | null;
  /** Placement changes sent to the server and not yet answered. */
  readonly pendingSaves: number;
  /** The ready request is in flight. */
  readonly readying: boolean;
  /** The cell in the opponent's waters the caller selected to fire at, awaiting confirmation. */
  readonly target: Cell | null;
  readonly shotStatus: ShotStatus;
  /** Why the last shot was refused or failed, until the next target is selected. */
  readonly shotError: FireShotError | null;
  /**
   * The shots drawn since the game was last loaded, from a response or an event: only these
   * animate in. Shots that came with a load (first render, refresh, reload) appear at rest.
   */
  readonly freshShots: FreshShots;
}

/** The cells of the shots drawn since the last load, per shooter. */
export interface FreshShots {
  readonly me: readonly Cell[];
  readonly opponent: readonly Cell[];
}

const NO_FRESH_SHOTS: FreshShots = { me: [], opponent: [] };

const initialState: GamePageState = {
  code: '',
  status: 'loading',
  game: null,
  activeKind: null,
  drag: null,
  pendingSaves: 0,
  readying: false,
  target: null,
  shotStatus: 'idle',
  shotError: null,
  freshShots: NO_FRESH_SHOTS,
};

/** The events after which the page reloads its game. */
export const GAME_RELOAD_EVENTS: readonly RealtimeEventType[] = [
  'game.joined.v1',
  'game.player-ready.v1',
  'game.started.v1',
];

/** Whether an event from the stream is about the game with `code` (its `gameCode`, any casing). */
export function isEventFor(event: RealtimeEvent, code: string): boolean {
  const payload = event.payload;
  return (
    typeof payload === 'object' &&
    payload !== null &&
    'gameCode' in payload &&
    typeof payload.gameCode === 'string' &&
    payload.gameCode.toUpperCase() === code.toUpperCase()
  );
}

/** Whether an event from the stream is about the game with `code` and changes what it shows. */
export function isReloadEventFor(event: RealtimeEvent, code: string): boolean {
  return GAME_RELOAD_EVENTS.includes(event.type) && isEventFor(event, code);
}

/** The shot a `game.shot-fired.v1` event reports, or `null` for any other or a malformed event. */
export function shotFiredFrom(event: RealtimeEvent): FiredShot | null {
  const p = event.payload;
  if (event.type !== 'game.shot-fired.v1' || typeof p !== 'object' || p === null) return null;
  if (
    !('playerId' in p && typeof p.playerId === 'string') ||
    !('column' in p && typeof p.column === 'number') ||
    !('row' in p && typeof p.row === 'number') ||
    !('hit' in p && typeof p.hit === 'boolean')
  ) {
    return null;
  }
  return { playerId: p.playerId, column: p.column, row: p.row, hit: p.hit };
}

/** The player a `game.turn-changed.v1` event gives the turn, or `null` for any other event. */
export function turnChangedFrom(event: RealtimeEvent): string | null {
  const p = event.payload;
  if (event.type !== 'game.turn-changed.v1' || typeof p !== 'object' || p === null) return null;
  return 'playerWithTurnId' in p && typeof p.playerWithTurnId === 'string'
    ? p.playerWithTurnId
    : null;
}

/**
 * Whether the caller may still change their fleet: the game is new, they are not ready and no
 * ready request is on its way.
 */
export function isEditable(state: GamePageState): boolean {
  return state.game?.state === 'new' && !state.game.me.ready && !state.readying;
}

/** Whether the caller may declare ready: all five ships placed and saved, not ready yet. */
export function canDeclareReady(state: GamePageState): boolean {
  return (
    isEditable(state) && state.pendingSaves === 0 && state.game?.me.ships.length === FLEET.length
  );
}

/** A game was requested for `code`: the page shows its loading state. */
export function loadStarted(state: GamePageState, code: string): GamePageState {
  return state.code === code
    ? { ...state, status: 'loading' }
    : { ...initialState, code, status: 'loading' };
}

/**
 * The server answered with the caller's view of the game. While placement changes are still on
 * their way the local fleet is kept, so an answer that predates them does not undo them on screen.
 */
export function gameLoaded(state: GamePageState, game: GameView): GamePageState {
  const shown =
    state.pendingSaves > 0 && state.game !== null
      ? { ...game, me: { ...game.me, ships: state.game.me.ships } }
      : game;
  // The server's view is the truth about the turn, so a shot waiting for the turn to move is freed.
  const shotStatus = state.shotStatus === 'fired' ? 'idle' : state.shotStatus;
  // Everything a load shows is at rest; only shots drawn after it animate.
  const next = {
    ...state,
    status: 'loaded' as const,
    game: shown,
    shotStatus,
    freshShots: NO_FRESH_SHOTS,
  };
  return withSelectableTarget(isEditable(next) ? next : { ...next, activeKind: null, drag: null });
}

/**
 * Loading failed. A missing game always shows as not found. Any other failure shows as failed
 * only when there is no game on screen yet; a failed reload keeps the game that is shown.
 */
export function loadFailed(state: GamePageState, error: GameApiError): GamePageState {
  if (error.kind === 'game-not-found') return { ...state, status: 'not-found', game: null };
  return state.game === null ? { ...state, status: 'failed' } : { ...state, status: 'loaded' };
}

/** The player selected a ship. */
export function shipActivated(state: GamePageState, kind: ShipKind): GamePageState {
  return isEditable(state) && state.activeKind !== kind ? { ...state, activeKind: kind } : state;
}

/** The player cleared the selection. */
export function activeCleared(state: GamePageState): GamePageState {
  return state.activeKind === null ? state : { ...state, activeKind: null };
}

/** A placement was accepted locally and sent: the fleet shows it at once. */
export function placementSent(state: GamePageState, ship: ShipView): GamePageState {
  if (state.game === null) return state;
  const me = { ...state.game.me, ships: withPlacement(state.game.me.ships, ship) };
  return { ...state, game: { ...state.game, me }, pendingSaves: state.pendingSaves + 1 };
}

/**
 * The server answered a placement. Only the answer to the last pending one replaces the fleet;
 * earlier answers do not know of the changes sent after them.
 */
export function placementSaved(state: GamePageState, game: GameView): GamePageState {
  const pendingSaves = Math.max(state.pendingSaves - 1, 0);
  return gameLoaded({ ...state, pendingSaves }, game);
}

/** The server refused a placement or did not answer; a reload follows. */
export function placementFailed(state: GamePageState): GamePageState {
  return { ...state, pendingSaves: Math.max(state.pendingSaves - 1, 0) };
}

/** A drag began. */
export function dragStarted(state: GamePageState, drag: Omit<ShipDrag, 'origin'>): GamePageState {
  return isEditable(state) ? { ...state, drag: { ...drag, origin: null } } : state;
}

/** The pointer is over `cell` (or off the board): the origin follows, minus the grab offset. */
export function draggedOver(state: GamePageState, cell: Cell | null): GamePageState {
  const drag = state.drag;
  if (drag === null) return state;
  const origin =
    cell === null
      ? null
      : drag.orientation === 'horizontal'
        ? { column: cell.column - drag.grabIndex, row: cell.row }
        : { column: cell.column, row: cell.row - drag.grabIndex };
  if (origin?.column === drag.origin?.column && origin?.row === drag.origin?.row) return state;
  return { ...state, drag: { ...drag, origin } };
}

/** The ship a drag would drop, or `null` while the pointer is off the board. */
export function draggedShip(drag: ShipDrag | null): ShipView | null {
  if (drag?.origin == null) return null;
  return { kind: drag.kind, orientation: drag.orientation, ...drag.origin };
}

/** Whether `shots` holds a shot at `cell`. */
export function hasShotAt(shots: readonly ShotView[], cell: Cell): boolean {
  return shots.some((shot) => shot.column === cell.column && shot.row === cell.row);
}

/** Whether the caller may fire now: the game is on, it is their turn and no shot is pending. */
export function canFire(state: GamePageState): boolean {
  return state.game?.state === 'started' && state.game.me.hasTurn && state.shotStatus === 'idle';
}

/** Whether `cell` may be the target: the caller may fire and has not fired at it before. */
export function isSelectable(state: GamePageState, cell: Cell): boolean {
  return canFire(state) && !hasShotAt(state.game?.me.shots ?? [], cell);
}

/** Drops a target that can no longer be fired at (the turn moved, the cell was shot meanwhile). */
function withSelectableTarget(state: GamePageState): GamePageState {
  const target = state.target;
  if (target === null || state.shotStatus === 'firing') return state;
  return isSelectable(state, target) ? state : { ...state, target: null };
}

/** The caller selected `cell` to fire at. Ignored while they may not fire or for a shot cell. */
export function targetSelected(state: GamePageState, cell: Cell): GamePageState {
  if (!isSelectable(state, cell)) return state;
  if (state.target?.column === cell.column && state.target.row === cell.row) return state;
  return { ...state, target: { column: cell.column, row: cell.row }, shotError: null };
}

/** The caller cancelled the target. */
export function targetCleared(state: GamePageState): GamePageState {
  return state.target === null || state.shotStatus === 'firing'
    ? state
    : { ...state, target: null };
}

/** The shot at the target was sent: no other shot until it is answered. */
export function shotSent(state: GamePageState): GamePageState {
  return { ...state, shotStatus: 'firing', shotError: null };
}

/** `shots` with `shot` added, unless a shot at the same cell is already there. */
function withShot(shots: readonly ShotView[], shot: ShotView): readonly ShotView[] {
  return hasShotAt(shots, shot)
    ? shots
    : [...shots, { column: shot.column, row: shot.row, hit: shot.hit }];
}

/**
 * The server recorded the caller's shot: it is drawn and the target cleared. The turn is left as
 * it is; it moves only with a turn change event or a reload, and until then no shot is possible.
 */
export function shotRecorded(state: GamePageState, shot: ShotView): GamePageState {
  if (state.game === null) return state;
  const shots = withShot(state.game.me.shots, shot);
  const me = { ...state.game.me, shots };
  // A turn change that arrived while the request was in flight already freed the next shot.
  const shotStatus = me.hasTurn ? 'fired' : 'idle';
  // The shot may already be drawn, by its own event arriving before the response.
  const freshShots =
    shots === state.game.me.shots ? state.freshShots : withFresh(state, 'me', shot);
  return { ...state, game: { ...state.game, me }, target: null, shotStatus, freshShots };
}

/** The fresh shots with `cell` added for `shooter`. */
function withFresh(state: GamePageState, shooter: keyof FreshShots, cell: Cell): FreshShots {
  const added = { column: cell.column, row: cell.row };
  return { ...state.freshShots, [shooter]: [...state.freshShots[shooter], added] };
}

/** The shot was refused or failed: nothing is drawn, the caller may select a cell again. */
export function shotRefused(state: GamePageState, reason: FireShotError): GamePageState {
  return { ...state, target: null, shotStatus: 'idle', shotError: reason };
}

/**
 * A shot event arrived: the shot is added to the shooter's shots, the caller's or the opponent's.
 * A shot the shooter already has at that cell (the caller's own echo, a replay) is not added again.
 */
export function shotFiredApplied(state: GamePageState, shot: FiredShot): GamePageState {
  const game = state.game;
  if (game === null) return state;
  if (shot.playerId === game.me.playerId) {
    const shots = withShot(game.me.shots, shot);
    if (shots === game.me.shots) return state;
    return withSelectableTarget({
      ...state,
      game: { ...game, me: { ...game.me, shots } },
      freshShots: withFresh(state, 'me', shot),
    });
  }
  if (game.opponent !== null && shot.playerId === game.opponent.playerId) {
    const shots = withShot(game.opponent.shots, shot);
    if (shots === game.opponent.shots) return state;
    return {
      ...state,
      game: { ...game, opponent: { ...game.opponent, shots } },
      freshShots: withFresh(state, 'opponent', shot),
    };
  }
  return state;
}

/** A turn change event arrived: the player it names has the turn, the other one has not. */
export function turnChanged(state: GamePageState, playerWithTurnId: string): GamePageState {
  const game = state.game;
  if (game === null) return state;
  const me = { ...game.me, hasTurn: game.me.playerId === playerWithTurnId };
  const opponent =
    game.opponent === null
      ? null
      : { ...game.opponent, hasTurn: game.opponent.playerId === playerWithTurnId };
  const shotStatus = state.shotStatus === 'fired' ? 'idle' : state.shotStatus;
  return withSelectableTarget({ ...state, game: { ...game, me, opponent }, shotStatus });
}

/**
 * The state of one game page: the caller's view of the game, how loading it went and the
 * placement or the shot in progress. Provided by `GamePage`, so it lives and dies with the page.
 *
 * Keeps the page current through the event stream: it reloads on a join, ready or start event for
 * its game and when the stream reconnects. Placement changes are shown at once and sent to the
 * server in order, one request per change; a refusal shows a message and reloads the game.
 *
 * During play the caller selects a target, confirms it and fires one shot per turn. A shot is
 * drawn only once the server recorded it (no optimistic markers); the turn moves only by a turn
 * change event or a reload.
 */
@Injectable()
export class GamePageStore {
  readonly #api = inject(GamesApi);
  readonly #events = inject(EventsService);
  readonly #snackBar = inject(MatSnackBar);
  readonly #state = signal<GamePageState>(initialState);

  /** The reload in flight, and whether another was asked for meanwhile. Not state: nothing renders them. */
  #reloading: Promise<void> | null = null;
  #reloadAgain = false;
  /** The tail of the save queue; each placement waits for the one before it. Not rendered. */
  #saves: Promise<void> = Promise.resolve();

  readonly state = this.#state.asReadonly();
  readonly code = computed(() => this.#state().code);
  readonly status = computed(() => this.#state().status);
  readonly game = computed(() => this.#state().game);
  /** The game's lifecycle state, `null` until a game is loaded. */
  readonly phase = computed(() => this.#state().game?.state ?? null);
  readonly me = computed(() => this.#state().game?.me ?? null);
  readonly opponent = computed(() => this.#state().game?.opponent ?? null);
  /** The caller's ships on the board. */
  readonly placedShips = computed(() => this.me()?.ships ?? NO_SHIPS);
  /** The ships of the fleet not yet on the board, in fleet order. */
  readonly unplacedFleet = computed(() => {
    const placed = new Set(this.placedShips().map((ship) => ship.kind));
    return FLEET.filter((ship) => !placed.has(ship.kind));
  });
  readonly activeKind = computed(() => this.#state().activeKind);
  readonly pendingSaves = computed(() => this.#state().pendingSaves);
  /** The active ship with what the screen says about it, or `null` when none is active. */
  readonly activeShip = computed(() => {
    const kind = this.activeKind();
    if (kind === null) return null;
    const name = nameOf(kind);
    return {
      kind,
      name,
      placed: this.placedShips().some((ship) => ship.kind === kind),
      rotateLabel: `Rotate ${name.toLowerCase()}`,
    };
  });
  /** Whether the fleet can still be changed; after ready the board is read only. */
  readonly canEdit = computed(() => isEditable(this.#state()));
  /** Whether the "Ready" action is enabled. */
  readonly canReady = computed(() => canDeclareReady(this.#state()));
  /** The caller declared ready (confirmed by the server). */
  readonly isReady = computed(() => this.me()?.ready ?? false);
  readonly readying = computed(() => this.#state().readying);
  /** The ship under the pointer during a drag, and whether it may be dropped there. */
  readonly preview = computed(() => {
    const ship = draggedShip(this.#state().drag);
    return ship === null ? null : { ship, valid: checkPlacement(ship, this.placedShips()).ok };
  });

  /** Whether the caller has the turn. */
  readonly myTurn = computed(() => this.me()?.hasTurn ?? false);
  /** The shots the caller fired, with their results. */
  readonly myShots = computed(() => this.me()?.shots ?? NO_SHOTS);
  /** The shots the opponent fired at the caller's board, with their results. */
  readonly opponentShots = computed(() => this.opponent()?.shots ?? NO_SHOTS);
  /** Whether a cell of the opponent's waters may be selected and fired at now. */
  readonly canShoot = computed(() => canFire(this.#state()));
  readonly shotStatus = computed(() => this.#state().shotStatus);
  /** The selected target, or `null`. */
  readonly target = computed(() => this.#state().target);
  /** The target's coordinate as the board labels it (`E7`), or `null` without a target. */
  readonly targetName = computed(() => {
    const target = this.target();
    return target === null ? null : cellLabel(target);
  });
  /** Why the last shot was refused or failed, for the page to tell the player. */
  readonly shotError = computed(() => this.#state().shotError);
  /** The cells of the caller's shots drawn since the last load: these animate in. */
  readonly myFreshShots = computed(() => this.#state().freshShots.me);
  /** The cells of the opponent's shots drawn since the last load: these animate in. */
  readonly opponentFreshShots = computed(() => this.#state().freshShots.opponent);

  constructor() {
    const destroyRef = inject(DestroyRef);

    // Only events for this game: a join, ready or start reloads it, shots and turns apply in place.
    this.#events.events$
      .pipe(
        filter((event) => isEventFor(event, this.code())),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((event) => this.#route(event));

    // Events sent while the stream was down are not all replayed, so a reconnect reloads.
    toObservable(this.#events.state)
      .pipe(
        pairwise(),
        filter(([previous, current]) => previous === 'reconnecting' && current === 'open'),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe(() => void this.reload());
  }

  /** Loads the game with `code`, showing the loading state until it arrives. */
  load(code: string): Promise<void> {
    this.#state.update((s) => loadStarted(s, code));
    return this.reload();
  }

  /** Loads the current game again, for example after a failure the player retries. */
  retry(): Promise<void> {
    return this.load(this.code());
  }

  /**
   * Requests the game again and shows the answer. Reloads never run in parallel: one asked for
   * while another is in flight runs once more after it, however often it was asked for.
   */
  reload(): Promise<void> {
    if (this.#reloading !== null) {
      this.#reloadAgain = true;
      return this.#reloading;
    }
    this.#reloading = this.#reloadUntilSettled().finally(() => (this.#reloading = null));
    return this.#reloading;
  }

  /** The player selected a ship, in the list or on the board. */
  activate(kind: ShipKind): void {
    this.#state.update((s) => shipActivated(s, kind));
  }

  /** The player cleared the selection (Escape). */
  clearActive(): void {
    this.#state.update(activeCleared);
  }

  /**
   * A cell was tapped or activated by keyboard. With an active ship, that ship is placed or moved
   * with its origin there, keeping its orientation (a ship from the list is horizontal). Without
   * one, a ship on the cell becomes active.
   */
  cellActivated(cell: Cell): void {
    const s = this.#state();
    if (!isEditable(s)) return;
    const kind = s.activeKind;
    if (kind === null) {
      const occupant = this.placedShips().find((ship) =>
        cellsOf(ship).some((c) => c.column === cell.column && c.row === cell.row),
      );
      if (occupant !== undefined) this.activate(occupant.kind);
      return;
    }
    const current = this.placedShips().find((ship) => ship.kind === kind);
    this.place({ kind, orientation: current?.orientation ?? 'horizontal', ...cell });
  }

  /**
   * Places or moves a ship. A placement that leaves the board or overlaps another ship is refused
   * at once with a message and nothing is sent. Answers whether the placement was accepted.
   */
  place(ship: ShipView): boolean {
    if (!isEditable(this.#state())) return false;
    const verdict = checkPlacement(ship, this.placedShips());
    const refusal = refusalMessage(ship.kind, verdict);
    if (refusal !== null) {
      this.#notify(refusal);
      return false;
    }
    this.#state.update((s) => placementSent(s, ship));
    const code = this.code();
    this.#saves = this.#saves.then(() => this.#save(code, ship));
    return true;
  }

  /**
   * Turns the active placed ship 90 degrees clockwise about its origin. A rotation that would
   * leave the board or overlap another ship is refused with a message and nothing is sent.
   */
  rotateActive(): void {
    const current = this.placedShips().find((ship) => ship.kind === this.activeKind());
    if (current !== undefined) this.place(rotated(current));
  }

  /** A drag of `kind` began, grabbed at its `grabIndex`-th cell. */
  dragStart(drag: Omit<ShipDrag, 'origin'>): void {
    this.#state.update((s) => dragStarted(s, drag));
  }

  /** The dragged ship's pointer is over `cell`, or off the board (`null`). */
  dragOver(cell: Cell | null): void {
    this.#state.update((s) => draggedOver(s, cell));
  }

  /**
   * The drag ended. A drop at a valid position places the ship there; anywhere else (an invalid
   * position, off the board) the ship stays where it was.
   */
  dragEnd(): void {
    const preview = this.preview();
    this.#state.update((s) => (s.drag === null ? s : { ...s, drag: null }));
    if (preview?.valid) this.place(preview.ship);
  }

  /**
   * Declares the caller's fleet ready. Only possible with five saved ships; while the request is
   * in flight nothing else is sent, however often this is called. On success the board becomes
   * read only; on a refusal a message is shown and the game reloaded.
   */
  async markReady(): Promise<void> {
    if (!canDeclareReady(this.#state())) return;
    const code = this.code();
    this.#state.update((s) => ({ ...s, readying: true, activeKind: null, drag: null }));
    try {
      const game = await this.#api.markReady(code);
      this.#state.update((s) =>
        s.code === code ? gameLoaded({ ...s, readying: false }, game) : s,
      );
    } catch (error) {
      this.#state.update((s) => ({ ...s, readying: false }));
      this.#notify(gameApiErrorMessage(error));
      await this.reload();
    }
  }

  /** The caller selected a cell of the opponent's waters to fire at. See `targetSelected`. */
  selectTarget(cell: Cell): void {
    this.#state.update((s) => targetSelected(s, cell));
  }

  /** The caller cancelled the selected target. */
  clearTarget(): void {
    this.#state.update(targetCleared);
  }

  /**
   * Fires at the selected target. One request per confirmed target: while it is in flight nothing
   * else is sent, however often this is called. A recorded shot is drawn and the target cleared;
   * a refused or failed one leaves the cell empty and the reason in `shotError`, and a `409`
   * reloads the game, since the server's view moved on.
   */
  async fireAtTarget(): Promise<void> {
    const s = this.#state();
    const target = s.target;
    if (target === null || !canFire(s)) return;
    const code = s.code;
    this.#state.update(shotSent);
    try {
      const shot = await this.#api.fireShot(code, target.column, target.row);
      this.#state.update((current) =>
        current.code === code ? shotRecorded(current, shot) : current,
      );
    } catch (error) {
      const reason = error instanceof FireShotFailure ? error.reason : 'failed';
      this.#state.update((current) => shotRefused(current, reason));
      if (isFireShotConflict(reason)) await this.reload();
    }
  }

  /** A `game.shot-fired.v1` event for this game arrived. */
  applyShotFired(shot: FiredShot): void {
    this.#state.update((s) => shotFiredApplied(s, shot));
  }

  /** A `game.turn-changed.v1` event for this game arrived. */
  applyTurnChanged(playerWithTurnId: string): void {
    this.#state.update((s) => turnChanged(s, playerWithTurnId));
  }

  /** Resolves once every placement sent so far has been answered. */
  settled(): Promise<void> {
    return this.#saves;
  }

  async #save(code: string, ship: ShipView): Promise<void> {
    try {
      const { column, row, orientation } = ship;
      const game = await this.#api.placeShip(code, ship.kind, { column, row, orientation });
      this.#state.update((s) => (s.code === code ? placementSaved(s, game) : s));
    } catch (error) {
      this.#state.update(placementFailed);
      this.#notify(gameApiErrorMessage(error));
      await this.reload();
    }
  }

  /** Hands an event for this game to the intent it drives. */
  #route(event: RealtimeEvent): void {
    if (GAME_RELOAD_EVENTS.includes(event.type)) {
      void this.reload();
      return;
    }
    const shot = shotFiredFrom(event);
    if (shot !== null) {
      this.applyShotFired(shot);
      return;
    }
    const playerWithTurnId = turnChangedFrom(event);
    if (playerWithTurnId !== null) this.applyTurnChanged(playerWithTurnId);
  }

  #notify(message: string): void {
    this.#snackBar.open(message, 'Dismiss', { duration: 5000 });
  }

  async #reloadUntilSettled(): Promise<void> {
    do {
      this.#reloadAgain = false;
      await this.#fetch();
    } while (this.#reloadAgain);
  }

  async #fetch(): Promise<void> {
    const code = this.code();
    try {
      const game = await this.#api.getGame(code);
      this.#state.update((s) => (s.code === code ? gameLoaded(s, game) : s));
      this.#connectEvents(game);
    } catch (error) {
      const failure: GameApiError = isGameApiError(error)
        ? error
        : { kind: 'unexpected', status: 0 };
      this.#state.update((s) => (s.code === code ? loadFailed(s, failure) : s));
    }
  }

  /** Opens the event stream for the caller. Idempotent: an open stream for them is kept. */
  #connectEvents(game: GameView): void {
    this.#events.connect(game.me.playerId);
  }
}
