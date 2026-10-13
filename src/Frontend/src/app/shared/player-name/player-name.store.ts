import { Injectable, computed, inject, signal } from '@angular/core';
import { ValidationError, form, schema, submit, validate } from '@angular/forms/signals';
import { PlayerSession } from '../../core/session/player-session';

/** The longest display name the Players service accepts. */
export const PLAYER_NAME_MAX_LENGTH = 40;

export const PLAYER_NAME_REQUIRED_MESSAGE = 'Enter a player name.';
export const PLAYER_NAME_TOO_LONG_MESSAGE = `Use at most ${PLAYER_NAME_MAX_LENGTH} characters.`;
export const PLAYER_NAME_CREATE_FAILED_MESSAGE =
  'We could not sign you on as a player. Check your connection and try again.';

export interface PlayerNameModel {
  readonly playerName: string;
}

export interface PlayerNameState {
  /** The message shown when creating the player failed; `null` otherwise. */
  readonly error: string | null;
}

/** The validation rule for a name, on its trimmed value: required, at most 40 characters. */
export function validatePlayerName(name: string): ValidationError | undefined {
  const trimmed = name.trim();
  if (trimmed.length === 0) return { kind: 'required', message: PLAYER_NAME_REQUIRED_MESSAGE };
  if (trimmed.length > PLAYER_NAME_MAX_LENGTH) {
    return { kind: 'maxLength', message: PLAYER_NAME_TOO_LONG_MESSAGE };
  }
  return undefined;
}

const playerNameSchema = schema<PlayerNameModel>((path) => {
  validate(path.playerName, ({ value }) => validatePlayerName(value()));
});

/**
 * The state of one player-name form: the Signal Forms model and validation, whether a create
 * request is in flight, and the retryable error of the last attempt. Provided by the component.
 */
@Injectable()
export class PlayerNameStore {
  readonly #session = inject(PlayerSession);

  readonly #model = signal<PlayerNameModel>({ playerName: '' });
  readonly #state = signal<PlayerNameState>({ error: null });

  /** The form tree the template binds with `[formField]`. */
  readonly form = form(this.#model, playerNameSchema);

  readonly error = computed(() => this.#state().error);
  readonly submitting = computed(() => this.form().submitting());
  /** The validation message for the name, once the field has been touched or submitted. */
  readonly nameError = computed(() => {
    const field = this.form.playerName();
    return field.touched() ? (field.errors()[0]?.message ?? null) : null;
  });

  /**
   * Validates and, when valid, creates the player with the trimmed name. Resolves `true` when the
   * player was created. While a request is pending a second call sends nothing and resolves
   * `false`. A failure keeps the typed name and sets a retryable error.
   */
  async submit(): Promise<boolean> {
    let created = false;
    await submit(this.form, async () => {
      this.#state.update((s) => ({ ...s, error: null }));
      try {
        await this.#session.createPlayer(this.#model().playerName.trim());
        created = true;
      } catch {
        this.#state.update((s) => ({ ...s, error: PLAYER_NAME_CREATE_FAILED_MESSAGE }));
      }
      return undefined;
    });
    return created;
  }
}
