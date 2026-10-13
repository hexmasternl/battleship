import { Injectable, computed, inject, signal } from '@angular/core';
import { ValidationError, form, schema, submit, validate } from '@angular/forms/signals';
import { UserSession } from '../../core/session/user-session';

/** The longest display name the Users service accepts. */
export const DISPLAY_NAME_MAX_LENGTH = 40;

export const DISPLAY_NAME_REQUIRED_MESSAGE = 'Enter a player name.';
export const DISPLAY_NAME_TOO_LONG_MESSAGE = `Use at most ${DISPLAY_NAME_MAX_LENGTH} characters.`;
export const DISPLAY_NAME_CREATE_FAILED_MESSAGE =
  'We could not sign you on as a player. Check your connection and try again.';

export interface DisplayNameModel {
  readonly displayName: string;
}

export interface DisplayNameState {
  /** The message shown when creating the user failed; `null` otherwise. */
  readonly error: string | null;
}

/** The validation rule for a name, on its trimmed value: required, at most 40 characters. */
export function validateDisplayName(name: string): ValidationError | undefined {
  const trimmed = name.trim();
  if (trimmed.length === 0) return { kind: 'required', message: DISPLAY_NAME_REQUIRED_MESSAGE };
  if (trimmed.length > DISPLAY_NAME_MAX_LENGTH) {
    return { kind: 'maxLength', message: DISPLAY_NAME_TOO_LONG_MESSAGE };
  }
  return undefined;
}

const displayNameSchema = schema<DisplayNameModel>((path) => {
  validate(path.displayName, ({ value }) => validateDisplayName(value()));
});

/**
 * The state of one display-name form: the Signal Forms model and validation, whether a create
 * request is in flight, and the retryable error of the last attempt. Provided by the component.
 */
@Injectable()
export class DisplayNameStore {
  readonly #session = inject(UserSession);

  readonly #model = signal<DisplayNameModel>({ displayName: '' });
  readonly #state = signal<DisplayNameState>({ error: null });

  /** The form tree the template binds with `[formField]`. */
  readonly form = form(this.#model, displayNameSchema);

  readonly error = computed(() => this.#state().error);
  readonly submitting = computed(() => this.form().submitting());
  /** The validation message for the name, once the field has been touched or submitted. */
  readonly nameError = computed(() => {
    const field = this.form.displayName();
    return field.touched() ? (field.errors()[0]?.message ?? null) : null;
  });

  /**
   * Validates and, when valid, creates the user with the trimmed name. Resolves `true` when the
   * user was created. While a request is pending a second call sends nothing and resolves
   * `false`. A failure keeps the typed name and sets a retryable error.
   */
  async submit(): Promise<boolean> {
    let created = false;
    await submit(this.form, async () => {
      this.#state.update((s) => ({ ...s, error: null }));
      try {
        await this.#session.createUser(this.#model().displayName.trim());
        created = true;
      } catch {
        this.#state.update((s) => ({ ...s, error: DISPLAY_NAME_CREATE_FAILED_MESSAGE }));
      }
      return undefined;
    });
    return created;
  }
}
