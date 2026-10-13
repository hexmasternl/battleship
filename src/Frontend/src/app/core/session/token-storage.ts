import { InjectionToken, Service, inject } from '@angular/core';

/** The token pair the Users service returns from both token endpoints. */
export interface TokenPair {
  readonly accessToken: string;
  /** ISO-8601 instant. */
  readonly accessTokenExpiresAt: string;
  readonly refreshToken: string;
  /** ISO-8601 instant. */
  readonly refreshTokenExpiresAt: string;
}

/** One stored token with the expiry instant the server gave it. */
export interface StoredToken {
  readonly value: string;
  /** ISO-8601 instant. */
  readonly expiresAt: string;
}

/** What the browser holds; a token is `null` when it is missing or storage is unavailable. */
export interface StoredTokens {
  readonly accessToken: StoredToken | null;
  readonly refreshToken: StoredToken | null;
}

export const REFRESH_TOKEN_KEY = 'battleship.refreshToken';
export const REFRESH_TOKEN_EXPIRES_AT_KEY = 'battleship.refreshTokenExpiresAt';
export const ACCESS_TOKEN_KEY = 'battleship.accessToken';
export const ACCESS_TOKEN_EXPIRES_AT_KEY = 'battleship.accessTokenExpiresAt';

/**
 * Returns the `Storage` that outlives the browser session. A function rather than the object,
 * because merely reading `window.localStorage` throws when the browser blocks storage.
 */
export const PERSISTENT_STORAGE = new InjectionToken<() => Storage>('PERSISTENT_STORAGE', {
  factory: () => () => localStorage,
});

/** Returns the `Storage` that ends with the browser session; see `PERSISTENT_STORAGE`. */
export const SESSION_STORAGE = new InjectionToken<() => Storage>('SESSION_STORAGE', {
  factory: () => () => sessionStorage,
});

/**
 * The user's tokens in Web Storage: the refresh token in `localStorage` so the identity survives
 * a browser restart, the access token in `sessionStorage` so it never outlives the session. Every
 * access is guarded: a browser that blocks storage reads as "no tokens" instead of throwing.
 */
@Service()
export class TokenStorage {
  readonly #persistent = inject(PERSISTENT_STORAGE);
  readonly #session = inject(SESSION_STORAGE);

  read(): StoredTokens {
    return {
      refreshToken: readToken(this.#persistent, REFRESH_TOKEN_KEY, REFRESH_TOKEN_EXPIRES_AT_KEY),
      accessToken: readToken(this.#session, ACCESS_TOKEN_KEY, ACCESS_TOKEN_EXPIRES_AT_KEY),
    };
  }

  /** Writes all four keys, replacing whatever was stored before. */
  store(pair: TokenPair): void {
    guarded(() => {
      const storage = this.#persistent();
      storage.setItem(REFRESH_TOKEN_KEY, pair.refreshToken);
      storage.setItem(REFRESH_TOKEN_EXPIRES_AT_KEY, pair.refreshTokenExpiresAt);
    });
    guarded(() => {
      const storage = this.#session();
      storage.setItem(ACCESS_TOKEN_KEY, pair.accessToken);
      storage.setItem(ACCESS_TOKEN_EXPIRES_AT_KEY, pair.accessTokenExpiresAt);
    });
  }

  /** Removes all four keys. */
  clear(): void {
    guarded(() => {
      const storage = this.#persistent();
      storage.removeItem(REFRESH_TOKEN_KEY);
      storage.removeItem(REFRESH_TOKEN_EXPIRES_AT_KEY);
    });
    guarded(() => {
      const storage = this.#session();
      storage.removeItem(ACCESS_TOKEN_KEY);
      storage.removeItem(ACCESS_TOKEN_EXPIRES_AT_KEY);
    });
  }
}

function readToken(
  storage: () => Storage,
  valueKey: string,
  expiresAtKey: string,
): StoredToken | null {
  return guarded(() => {
    const s = storage();
    const value = s.getItem(valueKey);
    const expiresAt = s.getItem(expiresAtKey);
    return value && expiresAt ? { value, expiresAt } : null;
  }, null);
}

function guarded(action: () => void): void;
function guarded<T>(action: () => T, fallback: T): T;
function guarded<T>(action: () => T, fallback?: T): T | undefined {
  try {
    return action();
  } catch {
    return fallback;
  }
}
