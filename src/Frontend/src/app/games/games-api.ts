import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { toGameApiError } from './game-api-error';
import {
  GameView,
  GameViewResponse,
  ShipKind,
  ShipPlacementRequest,
  toGameView,
} from './game-view';

export const GAMES_URL = '/api/games';

/** The URL that joins the game with `code`: `POST /api/games/{code}/join`. */
export function joinGameUrl(code: string): string {
  return `${GAMES_URL}/${encodeURIComponent(code)}/join`;
}

/** The answer to creating a game: its id and the code players share to join it. */
export interface CreateGameResponse {
  readonly gameId: string;
  readonly gameCode: string;
}

/** The URL of one game: `/api/games/{code}`. */
export function gameUrl(code: string): string {
  return `${GAMES_URL}/${encodeURIComponent(code)}`;
}

/** The answer to joining a game: the game the player now plays in. */
export interface JoinGameResponse {
  readonly gameId: string;
  readonly gameCode: string;
}

/**
 * Why a join was refused: no such game, both seats taken, already started, or anything else
 * (including a lost update race and network errors), which is worth trying again.
 */
export type JoinGameError = 'not-found' | 'full' | 'started' | 'failed';

/** The rejection of `GamesApi.joinGame`, carrying the reason the dialog names. */
export class JoinGameFailure extends Error {
  constructor(readonly reason: JoinGameError) {
    super(`Joining the game failed: ${reason}`);
    this.name = 'JoinGameFailure';
  }
}

/**
 * Maps a failed join request onto a `JoinGameError`. A `409` is told apart by the trailing slug of
 * its problem details `type` (`https://battleship.hexmaster.nl/problems/<slug>`).
 */
export function toJoinGameError(error: unknown): JoinGameError {
  if (!(error instanceof HttpErrorResponse)) return 'failed';
  if (error.status === 404) return 'not-found';
  if (error.status !== 409) return 'failed';
  switch (problemSlug(error.error)) {
    case 'game-full':
      return 'full';
    case 'game-already-started':
      return 'started';
    default:
      return 'failed';
  }
}

function problemSlug(body: unknown): string | null {
  if (typeof body !== 'object' || body === null || !('type' in body)) return null;
  const type = body.type;
  return typeof type === 'string' ? type.slice(type.lastIndexOf('/') + 1) : null;
}

/**
 * The Games service. Requests carry the player's access token (`authInterceptor`) and go to the
 * gateway (`apiGatewayInterceptor`).
 *
 * The game calls (`getGame`, `placeShip`, `markReady`) resolve the caller's `GameView` and reject
 * with a `GameApiError` (see `game-api-error.ts`), never with the raw HTTP error.
 */
@Service()
export class GamesApi {
  readonly #http = inject(HttpClient);

  /** `POST /api/games`: creates a game with the current player as player one. No body. */
  createGame(): Promise<CreateGameResponse> {
    return firstValueFrom(this.#http.post<CreateGameResponse>(GAMES_URL, null));
  }

  /**
   * `POST /api/games/{code}/join`: seats the current player in the game, or confirms the seat
   * they already have. No body. Rejects with a `JoinGameFailure` naming the reason.
   */
  async joinGame(code: string): Promise<JoinGameResponse> {
    try {
      return await firstValueFrom(this.#http.post<JoinGameResponse>(joinGameUrl(code), null));
    } catch (error) {
      throw new JoinGameFailure(toJoinGameError(error));
    }
  }

  /** `GET /api/games/{code}`: the caller's view of the game. */
  getGame(code: string): Promise<GameView> {
    return this.#view(this.#http.get<GameViewResponse>(gameUrl(code)));
  }

  /** `PUT /api/games/{code}/ships/{kind}`: places or moves one of the caller's ships. */
  placeShip(code: string, kind: ShipKind, placement: ShipPlacementRequest): Promise<GameView> {
    const body: ShipPlacementRequest = {
      column: placement.column,
      row: placement.row,
      orientation: placement.orientation,
    };
    return this.#view(this.#http.put<GameViewResponse>(`${gameUrl(code)}/ships/${kind}`, body));
  }

  /** `POST /api/games/{code}/ready`: declares the caller's fleet ready. No body. */
  markReady(code: string): Promise<GameView> {
    return this.#view(this.#http.post<GameViewResponse>(`${gameUrl(code)}/ready`, null));
  }

  async #view(request: Observable<GameViewResponse>): Promise<GameView> {
    try {
      return toGameView(await firstValueFrom(request));
    } catch (error) {
      throw toGameApiError(error);
    }
  }
}
