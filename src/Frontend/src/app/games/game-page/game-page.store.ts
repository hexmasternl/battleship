import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { filter, pairwise } from 'rxjs';
import { EventsService } from '../../events/events.service';
import { RealtimeEvent } from '../../events/realtime-event';
import { RealtimeEventType } from '../../events/realtime-event-types';
import { GameApiError, gameApiErrorMessage, isGameApiError } from '../game-api-error';
import { GameView, Orientation, ShipKind, ShipView } from '../game-view';
import { GamesApi } from '../games-api';
import {
  Cell,
  FLEET,
  cellsOf,
  checkPlacement,
  nameOf,
  refusalMessage,
  rotated,
  withPlacement,
} from '../placement/placement-rules';

const NO_SHIPS: readonly ShipView[] = [];

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
}

const initialState: GamePageState = {
  code: '',
  status: 'loading',
  game: null,
  activeKind: null,
  drag: null,
  pendingSaves: 0,
};

/** The events after which the page reloads its game. */
export const GAME_RELOAD_EVENTS: readonly RealtimeEventType[] = [
  'game.joined.v1',
  'game.player-ready.v1',
  'game.started.v1',
];

/** Whether an event from the stream is about the game with `code` and changes what it shows. */
export function isReloadEventFor(event: RealtimeEvent, code: string): boolean {
  if (!GAME_RELOAD_EVENTS.includes(event.type)) return false;
  const payload = event.payload;
  return (
    typeof payload === 'object' &&
    payload !== null &&
    'gameCode' in payload &&
    typeof payload.gameCode === 'string' &&
    payload.gameCode.toUpperCase() === code.toUpperCase()
  );
}

/** Whether the caller may still change their fleet: the game is new and they are not ready. */
export function isEditable(state: GamePageState): boolean {
  return state.game?.state === 'new' && !state.game.me.ready;
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
  const next = { ...state, status: 'loaded' as const, game: shown };
  return isEditable(next) ? next : { ...next, activeKind: null, drag: null };
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

/**
 * The state of one game page: the caller's view of the game, how loading it went and the
 * placement in progress. Provided by `GamePage`, so it lives and dies with the page.
 *
 * Keeps the page current through the event stream: it reloads on a join, ready or start event for
 * its game and when the stream reconnects. Placement changes are shown at once and sent to the
 * server in order, one request per change; a refusal shows a message and reloads the game.
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
  /** The ship under the pointer during a drag, and whether it may be dropped there. */
  readonly preview = computed(() => {
    const ship = draggedShip(this.#state().drag);
    return ship === null ? null : { ship, valid: checkPlacement(ship, this.placedShips()).ok };
  });

  constructor() {
    const destroyRef = inject(DestroyRef);

    this.#events.events$
      .pipe(
        filter((event) => isReloadEventFor(event, this.code())),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe(() => void this.reload());

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
