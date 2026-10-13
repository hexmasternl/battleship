import { HttpErrorResponse } from '@angular/common/http';
import { InjectionToken, Service, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UsersApi } from './users-api';
import { StoredToken, TokenStorage } from './token-storage';

/**
 * `unknown`: no tokens obtained in this page load. `restoring`: a refresh is in flight.
 * `known`: tokens came from a successful refresh or from creating a user.
 */
export type UserStatus = 'unknown' | 'restoring' | 'known';

export interface UserSessionState {
  readonly status: UserStatus;
}

const initialState: UserSessionState = { status: 'unknown' };

/** The current time in epoch milliseconds; replaceable in tests. */
export const SESSION_CLOCK = new InjectionToken<() => number>('SESSION_CLOCK', {
  factory: () => () => Date.now(),
});

/**
 * Whether a stored token's expiry instant has passed, or will pass within `marginMs`. An
 * unreadable instant counts as expired.
 */
export function isExpired(token: StoredToken, now: number, marginMs = 0): boolean {
  const expiresAt = Date.parse(token.expiresAt);
  return Number.isNaN(expiresAt) || expiresAt <= now + marginMs;
}

/**
 * The visitor's identity for this page load, shared by the whole application.
 *
 * `restore()` exchanges a stored refresh token for a new pair; `createUser()` creates a user;
 * `ensureUser()` answers whether the visitor is a known user, waiting for or retrying a
 * restore first. The tokens themselves live in `TokenStorage`.
 */
@Service()
export class UserSession {
  readonly #api = inject(UsersApi);
  readonly #storage = inject(TokenStorage);
  readonly #now = inject(SESSION_CLOCK);

  readonly #state = signal<UserSessionState>(initialState);
  /** The restore in flight, shared by concurrent callers. Not state: nothing renders it. */
  #restoring: Promise<void> | null = null;

  readonly state = this.#state.asReadonly();
  readonly status = computed(() => this.#state().status);
  readonly isKnown = computed(() => this.#state().status === 'known');

  /**
   * Refreshes the stored refresh token. Never rejects. Without a stored token nothing happens; an
   * expired one is cleared without a request. A `401` clears the tokens unless another tab has
   * rotated them meanwhile, in which case the new stored token is tried once. A network or server
   * failure keeps the refresh token so a later call can try again.
   */
  restore(): Promise<void> {
    if (this.#restoring) return this.#restoring;

    const stored = this.#storage.read().refreshToken;
    if (stored === null) return Promise.resolve();
    if (isExpired(stored, this.#now())) {
      this.#storage.clear();
      this.#setStatus('unknown');
      return Promise.resolve();
    }

    this.#setStatus('restoring');
    this.#restoring = this.#refresh(stored.value, true).finally(() => {
      this.#restoring = null;
    });
    return this.#restoring;
  }

  /**
   * Renews the token pair because the access token expired or was refused. Shares a refresh in
   * flight with every other caller, exactly like `restore()`, and follows the same rules when the
   * refresh is refused. Never rejects; resolves whether the visitor is a known user afterwards.
   */
  async renew(): Promise<boolean> {
    await this.restore();
    return this.isKnown();
  }

  /**
   * Creates a user with the given display name and stores the returned pair. Rejects with the
   * HTTP error when creation fails; the state is then unchanged and no tokens are stored.
   */
  async createUser(displayName: string): Promise<void> {
    const pair = await firstValueFrom(this.#api.createTokens(displayName));
    this.#storage.store(pair);
    this.#setStatus('known');
  }

  /**
   * Whether the visitor is a known user, after waiting for a restore in flight and, when the
   * visitor is still unknown but a refresh token is stored, after one more restore attempt.
   */
  async ensureUser(): Promise<boolean> {
    if (this.#restoring) await this.#restoring;
    if (!this.isKnown() && this.#storage.read().refreshToken !== null) await this.restore();
    return this.isKnown();
  }

  async #refresh(refreshToken: string, mayRetry: boolean): Promise<void> {
    try {
      const pair = await firstValueFrom(this.#api.refreshTokens(refreshToken));
      this.#storage.store(pair);
      this.#setStatus('known');
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        // Network or server failure: possibly transient, so the refresh token stays.
        this.#setStatus('unknown');
        return;
      }
      const current = this.#storage.read().refreshToken;
      if (current?.value === refreshToken) {
        this.#storage.clear();
      } else if (current !== null && mayRetry && !isExpired(current, this.#now())) {
        // Another tab rotated the token after this one read it: try once with the new value.
        return this.#refresh(current.value, false);
      }
      this.#setStatus('unknown');
    }
  }

  #setStatus(status: UserStatus): void {
    this.#state.update((s) => (s.status === status ? s : { ...s, status }));
  }
}
