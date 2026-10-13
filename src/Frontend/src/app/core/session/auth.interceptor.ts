import {
  HttpContextToken,
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, defer, switchMap, throwError } from 'rxjs';
import { UserSession, SESSION_CLOCK, isExpired } from './user-session';
import { CREATE_TOKENS_URL } from './users-api';
import { TokenStorage } from './token-storage';

const API_PREFIX = '/api/';

/** An access token this close to its expiry is renewed before a request is sent with it. */
export const ACCESS_TOKEN_RENEWAL_MARGIN_MS = 30_000;

/** Marks the one repeat of a refused request, so a repeat is never repeated again. */
export const RETRIED_AFTER_RENEWAL = new HttpContextToken<boolean>(() => false);

/**
 * The token endpoints (`/api/users/tokens` and everything below it, such as `/refresh`) are
 * anonymous; sending a bearer token there, or renewing before them, would loop.
 */
function isTokenEndpoint(url: string): boolean {
  const path = url.split('?')[0];
  return path === CREATE_TOKENS_URL || path.startsWith(`${CREATE_TOKENS_URL}/`);
}

function withBearer<T>(req: HttpRequest<T>, token: string | null): HttpRequest<T> {
  return token === null ? req : req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * Authenticates backend requests (`/api/...`) with the user's access token. Registered before the
 * gateway interceptor, which rewrites the `/api` prefix this one matches on.
 *
 * - An access token that is missing or expires within 30 seconds is renewed first through
 *   `UserSession.renew()` when a refresh token is stored; concurrent requests share one renewal.
 * - A `401` on a request that carried a token renews once and repeats the request once, marked with
 *   `RETRIED_AFTER_RENEWAL`. A failed renewal or a second `401` reaches the caller as the error.
 * - The token endpoints and requests outside `/api/` pass through untouched.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_PREFIX) || isTokenEndpoint(req.url)) return next(req);

  const session = inject(UserSession);
  const storage = inject(TokenStorage);
  const now = inject(SESSION_CLOCK);

  /** The access token to send, renewed first when it is missing or about to expire. */
  async function tokenToSend(): Promise<string | null> {
    const { accessToken, refreshToken } = storage.read();
    if (accessToken !== null && !isExpired(accessToken, now(), ACCESS_TOKEN_RENEWAL_MARGIN_MS)) {
      return accessToken.value;
    }
    if (refreshToken !== null) {
      await session.renew();
      return usableAccessToken();
    }
    return accessToken !== null && !isExpired(accessToken, now()) ? accessToken.value : null;
  }

  /** A new access token to replace the refused one, or `null` when renewal produced none. */
  async function tokenReplacing(refused: string): Promise<string | null> {
    // A concurrent request may already have renewed the pair after this one was sent.
    const current = usableAccessToken();
    if (current !== null && current !== refused) return current;
    await session.renew();
    const renewed = usableAccessToken();
    return renewed !== refused ? renewed : null;
  }

  function usableAccessToken(): string | null {
    const { accessToken } = storage.read();
    return accessToken !== null && !isExpired(accessToken, now()) ? accessToken.value : null;
  }

  // `defer` so the renewal starts on subscription, like the request itself.
  return defer(() => tokenToSend()).pipe(
    switchMap((token) =>
      next(withBearer(req, token)).pipe(
        catchError((error: unknown) => {
          const refused = error instanceof HttpErrorResponse && error.status === 401;
          if (!refused || token === null || req.context.get(RETRIED_AFTER_RENEWAL)) {
            return throwError(() => error);
          }
          return defer(() => tokenReplacing(token)).pipe(
            switchMap((renewed) => {
              if (renewed === null) return throwError(() => error);
              // The original request is not used again, so marking its context is safe.
              const repeat = req.clone({ context: req.context.set(RETRIED_AFTER_RENEWAL, true) });
              return next(withBearer(repeat, renewed));
            }),
          );
        }),
      ),
    ),
  );
};
