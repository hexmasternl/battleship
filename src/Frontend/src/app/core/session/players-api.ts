import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TokenPair } from './token-storage';

export const CREATE_TOKENS_URL = '/api/users/tokens';
export const REFRESH_TOKENS_URL = '/api/users/tokens/refresh';

/**
 * The anonymous token endpoints of the Players service. Both answer with a token pair; refreshing
 * answers `401` when the refresh token is refused. The gateway interceptor routes `/api/...`.
 */
@Service()
export class PlayersApi {
  readonly #http = inject(HttpClient);

  /** `POST /api/users/tokens`: creates a player with the given display name. */
  createTokens(displayName: string): Observable<TokenPair> {
    return this.#http.post<TokenPair>(CREATE_TOKENS_URL, { displayName });
  }

  /** `POST /api/users/tokens/refresh`: exchanges a refresh token for a new, rotated pair. */
  refreshTokens(refreshToken: string): Observable<TokenPair> {
    return this.#http.post<TokenPair>(REFRESH_TOKENS_URL, { refreshToken });
  }
}
