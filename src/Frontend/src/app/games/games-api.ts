import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export const GAMES_URL = '/api/games';

/** The answer to creating a game: its id and the code players share to join it. */
export interface CreateGameResponse {
  readonly gameId: string;
  readonly gameCode: string;
}

/**
 * The Games service. Requests carry the player's access token (`authInterceptor`) and go to the
 * gateway (`apiGatewayInterceptor`).
 */
@Service()
export class GamesApi {
  readonly #http = inject(HttpClient);

  /** `POST /api/games`: creates a game with the current player as player one. No body. */
  createGame(): Promise<CreateGameResponse> {
    return firstValueFrom(this.#http.post<CreateGameResponse>(GAMES_URL, null));
  }
}
