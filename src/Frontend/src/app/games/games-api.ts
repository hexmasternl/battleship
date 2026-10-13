import { HttpClient } from '@angular/common/http';
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

/** The answer to creating a game: its id and the code players share to join it. */
export interface CreateGameResponse {
  readonly gameId: string;
  readonly gameCode: string;
}

/** The URL of one game: `/api/games/{code}`. */
export function gameUrl(code: string): string {
  return `${GAMES_URL}/${encodeURIComponent(code)}`;
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
