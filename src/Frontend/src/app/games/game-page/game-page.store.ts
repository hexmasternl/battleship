import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { filter, pairwise } from 'rxjs';
import { EventsService } from '../../events/events.service';
import { RealtimeEvent } from '../../events/realtime-event';
import { RealtimeEventType } from '../../events/realtime-event-types';
import { GameApiError, isGameApiError } from '../game-api-error';
import { GameView, ShipView } from '../game-view';
import { GamesApi } from '../games-api';
import { FLEET } from '../placement/placement-rules';

const NO_SHIPS: readonly ShipView[] = [];

/** Where the page is with loading its game. */
export type GamePageStatus = 'loading' | 'loaded' | 'not-found' | 'failed';

export interface GamePageState {
  readonly code: string;
  readonly status: GamePageStatus;
  /** The caller's view as the server last answered it, with local changes applied on top. */
  readonly game: GameView | null;
}

const initialState: GamePageState = { code: '', status: 'loading', game: null };

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

/** A game was requested for `code`: the page shows its loading state. */
export function loadStarted(state: GamePageState, code: string): GamePageState {
  return { ...state, code, status: 'loading', game: state.code === code ? state.game : null };
}

/** The server answered with the caller's view of the game. */
export function gameLoaded(state: GamePageState, game: GameView): GamePageState {
  return { ...state, status: 'loaded', game };
}

/**
 * Loading failed. A missing game always shows as not found. Any other failure shows as failed
 * only when there is no game on screen yet; a failed reload keeps the game that is shown.
 */
export function loadFailed(state: GamePageState, error: GameApiError): GamePageState {
  if (error.kind === 'game-not-found') return { ...state, status: 'not-found', game: null };
  return state.game === null ? { ...state, status: 'failed' } : { ...state, status: 'loaded' };
}

/**
 * The state of one game page: the caller's view of the game and how loading it went. Provided by
 * `GamePage`, so it lives and dies with the page. Keeps the page current through the event
 * stream: it reloads on a join, ready or start event for its game and when the stream reconnects.
 */
@Injectable()
export class GamePageStore {
  readonly #api = inject(GamesApi);
  readonly #events = inject(EventsService);
  readonly #state = signal<GamePageState>(initialState);

  /** The reload in flight, and whether another was asked for meanwhile. Not state: nothing renders them. */
  #reloading: Promise<void> | null = null;
  #reloadAgain = false;

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
