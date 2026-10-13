import { Injectable, computed, inject, signal } from '@angular/core';
import { CreateGameResponse, GamesApi } from '../games-api';

export const CREATE_GAME_FAILED_MESSAGE =
  'The game could not be created. Check your connection and try again.';

export type CreateGameStatus = 'idle' | 'creating' | 'failed';

export interface CreateGameState {
  readonly status: CreateGameStatus;
  /** The message shown after a failed attempt; `null` otherwise. */
  readonly error: string | null;
}

const initialState: CreateGameState = { status: 'idle', error: null };

/** A create request was sent: the error of an earlier attempt is gone. */
export function creationStarted(state: CreateGameState): CreateGameState {
  return state.status === 'creating' ? state : { status: 'creating', error: null };
}

/** The create request failed: the dialog offers to try again. */
export function creationFailed(state: CreateGameState, message: string): CreateGameState {
  return { ...state, status: 'failed', error: message };
}

/**
 * The state of one create-game confirmation: idle, a request in flight, or a retryable failure.
 * Provided by `CreateGameDialog`, so it lives and dies with the dialog.
 */
@Injectable()
export class CreateGameStore {
  readonly #api = inject(GamesApi);
  readonly #state = signal<CreateGameState>(initialState);

  readonly state = this.#state.asReadonly();
  readonly status = computed(() => this.#state().status);
  readonly error = computed(() => this.#state().error);
  readonly isCreating = computed(() => this.#state().status === 'creating');
  readonly hasFailed = computed(() => this.#state().status === 'failed');

  /**
   * Creates the game. Resolves the response on success and `null` on failure, which leaves a
   * retryable error. While a request is in flight a second call sends nothing and resolves `null`.
   */
  async create(): Promise<CreateGameResponse | null> {
    if (this.isCreating()) return null;
    this.#state.update(creationStarted);
    try {
      const created = await this.#api.createGame();
      this.#state.set(initialState);
      return created;
    } catch {
      this.#state.update((s) => creationFailed(s, CREATE_GAME_FAILED_MESSAGE));
      return null;
    }
  }
}
