import { Injectable, computed, inject, signal } from '@angular/core';
import { isGameCode, normalizeGameCode } from '../game-code';
import { GamesApi, JoinGameError, JoinGameFailure, JoinGameResponse } from '../games-api';

export const JOIN_GAME_FULL_MESSAGE = 'This game already has two players.';
export const JOIN_GAME_STARTED_MESSAGE = 'This game has already started.';
export const JOIN_GAME_FAILED_MESSAGE = 'Something went wrong. Try again.';

/** The message for a game that does not exist, naming the code the player entered. */
export function joinGameNotFoundMessage(code: string): string {
  return `No game with code ${code} was found.`;
}

export type JoinGameStatus = 'idle' | 'joining' | 'failed';

export interface JoinGameState {
  /** The normalized game code: upper case, `A`-`Z` and `0`-`9` only, at most 8 characters. */
  readonly code: string;
  readonly status: JoinGameStatus;
  /** Why the last attempt was refused; `null` when there is nothing to report. */
  readonly error: JoinGameError | null;
}

export const initialJoinGameState: JoinGameState = { code: '', status: 'idle', error: null };

/** The player edited the code: it is normalized and the error of an earlier attempt is gone. */
export function codeEdited(state: JoinGameState, raw: string): JoinGameState {
  if (state.status === 'joining') return state;
  const code = normalizeGameCode(raw);
  if (code === state.code && state.error === null) return state;
  return { code, status: 'idle', error: null };
}

/** A join request was sent: the error of an earlier attempt is gone. */
export function joinStarted(state: JoinGameState): JoinGameState {
  return state.status === 'joining' ? state : { ...state, status: 'joining', error: null };
}

/** The join request was refused or failed; the code stays for correction or a retry. */
export function joinFailed(state: JoinGameState, error: JoinGameError): JoinGameState {
  return { ...state, status: 'failed', error };
}

/** The sentence the dialog shows for `error`. */
export function joinErrorMessage(error: JoinGameError, code: string): string {
  switch (error) {
    case 'not-found':
      return joinGameNotFoundMessage(code);
    case 'full':
      return JOIN_GAME_FULL_MESSAGE;
    case 'started':
      return JOIN_GAME_STARTED_MESSAGE;
    case 'failed':
      return JOIN_GAME_FAILED_MESSAGE;
  }
}

/**
 * The state of one join-game dialog: the code being typed, whether a join request is in flight,
 * and why the last attempt was refused. Provided by `JoinGameDialog`, so it lives and dies with
 * the dialog.
 */
@Injectable()
export class JoinGameStore {
  readonly #api = inject(GamesApi);
  readonly #state = signal<JoinGameState>(initialJoinGameState);

  readonly state = this.#state.asReadonly();
  readonly code = computed(() => this.#state().code);
  readonly status = computed(() => this.#state().status);
  readonly error = computed(() => this.#state().error);
  readonly isJoining = computed(() => this.#state().status === 'joining');
  readonly canJoin = computed(() => !this.isJoining() && isGameCode(this.#state().code));
  /** The sentence for the current error; `null` when there is none. */
  readonly errorMessage = computed(() => {
    const { error, code } = this.#state();
    return error === null ? null : joinErrorMessage(error, code);
  });

  /** The player typed or pasted into the code input. Ignored while a join is in flight. */
  edit(raw: string): void {
    this.#state.update((s) => codeEdited(s, raw));
  }

  /**
   * Joins the game with the current code. Resolves the response on success and `null` when the
   * join was refused or failed, which leaves the reason in `error`. Without a valid code, or while
   * a request is in flight, a call sends nothing and resolves `null`.
   */
  async join(): Promise<JoinGameResponse | null> {
    if (!this.canJoin()) return null;
    this.#state.update(joinStarted);
    try {
      const joined = await this.#api.joinGame(this.#state().code);
      this.#state.update((s) => ({ ...s, status: 'idle' }));
      return joined;
    } catch (e) {
      const reason = e instanceof JoinGameFailure ? e.reason : 'failed';
      this.#state.update((s) => joinFailed(s, reason));
      return null;
    }
  }
}
