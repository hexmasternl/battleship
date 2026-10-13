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

/** The answer to a recorded shot: the cell fired at and whether it hit a ship. */
export interface FireShotResponse {
  readonly column: number;
  readonly row: number;
  readonly hit: boolean;
}

/**
 * Why a shot was refused: one of the `409` reasons, no such game (or not a player of it), or
 * anything else (a validation error, a server error, no answer at all).
 */
export type FireShotError =
  | 'game-not-started'
  | 'not-your-turn'
  | 'cell-already-shot'
  | 'game-update-conflict'
  | 'not-found'
  | 'failed';

/** The rejection of `GamesApi.fireShot`, carrying the reason the page names. */
export class FireShotFailure extends Error {
  constructor(readonly reason: FireShotError) {
    super(`Firing the shot failed: ${reason}`);
    this.name = 'FireShotFailure';
  }
}

const FIRE_SHOT_CONFLICTS: readonly FireShotError[] = [
  'game-not-started',
  'not-your-turn',
  'cell-already-shot',
  'game-update-conflict',
];

/** Maps a failed shot request onto a `FireShotError`; a `409` by the slug of its problem type. */
export function toFireShotError(error: unknown): FireShotError {
  if (!(error instanceof HttpErrorResponse)) return 'failed';
  if (error.status === 404) return 'not-found';
  if (error.status !== 409) return 'failed';
  const slug = problemSlug(error.error);
  return FIRE_SHOT_CONFLICTS.find((reason) => reason === slug) ?? 'failed';
}

const FIRE_SHOT_MESSAGES: Record<FireShotError, string> = {
  'game-not-started': 'The game has not started yet.',
  'not-your-turn': 'It is not your turn.',
  'cell-already-shot': 'You already fired at that cell.',
  'game-update-conflict': 'The game changed at the same moment. Please try again.',
  'not-found': 'This game could not be found.',
  failed: 'The shot was not fired. Please try again.',
};

/** The message a player sees when a shot was refused or failed. */
export function fireShotErrorMessage(reason: FireShotError): string {
  return FIRE_SHOT_MESSAGES[reason];
}

/** Whether the server refused a shot with a `409`: the game moved on and is reloaded. */
export function isFireShotConflict(reason: FireShotError): boolean {
  return FIRE_SHOT_CONFLICTS.includes(reason);
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

  /**
   * `POST /api/games/{code}/shots`: fires the caller's shot at a zero-based cell. Resolves with the
   * result; rejects with a `FireShotFailure` naming the reason.
   */
  async fireShot(code: string, column: number, row: number): Promise<FireShotResponse> {
    try {
      return await firstValueFrom(
        this.#http.post<FireShotResponse>(`${gameUrl(code)}/shots`, { column, row }),
      );
    } catch (error) {
      throw new FireShotFailure(toFireShotError(error));
    }
  }

  async #view(request: Observable<GameViewResponse>): Promise<GameView> {
    try {
      return toGameView(await firstValueFrom(request));
    } catch (error) {
      throw toGameApiError(error);
    }
  }
}
