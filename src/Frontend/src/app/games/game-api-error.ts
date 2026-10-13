import { HttpErrorResponse } from '@angular/common/http';

/** The problem slugs the Games API answers with (`type` ends in `/problems/<slug>`). */
export const GAME_PROBLEM_SLUGS = [
  'game-not-found',
  'placement-out-of-board',
  'placement-overlap',
  'player-ready',
  'game-already-started',
  'fleet-incomplete',
  'game-update-conflict',
] as const;

export type GameProblemSlug = (typeof GAME_PROBLEM_SLUGS)[number];

/**
 * Why a game request failed: one of the problem slugs, `network` when no answer arrived, or
 * `unexpected` for any other answer (a validation error, a server error, an unknown problem).
 */
export type GameApiErrorKind = GameProblemSlug | 'network' | 'unexpected';

/** The error every `GamesApi` game call rejects with. */
export interface GameApiError {
  readonly kind: GameApiErrorKind;
  /** The HTTP status, `0` when no answer arrived. */
  readonly status: number;
}

const PROBLEM_TYPE = /\/problems\/([a-z0-9-]+)$/;

function isProblemSlug(value: string): value is GameProblemSlug {
  return (GAME_PROBLEM_SLUGS as readonly string[]).includes(value);
}

/** Maps whatever a request failed with onto a `GameApiError`. */
export function toGameApiError(error: unknown): GameApiError {
  if (!(error instanceof HttpErrorResponse)) return { kind: 'unexpected', status: 0 };
  if (error.status === 0) return { kind: 'network', status: 0 };

  const body: unknown = error.error;
  const type =
    typeof body === 'object' && body !== null && 'type' in body && typeof body.type === 'string'
      ? body.type
      : '';
  const slug = PROBLEM_TYPE.exec(type)?.[1] ?? '';
  if (isProblemSlug(slug)) return { kind: slug, status: error.status };
  // A 404 without a problem body (for example from the gateway) still means "no such game".
  if (error.status === 404) return { kind: 'game-not-found', status: 404 };
  return { kind: 'unexpected', status: error.status };
}

export function isGameApiError(error: unknown): error is GameApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'kind' in error &&
    'status' in error &&
    typeof error.status === 'number'
  );
}

const MESSAGES: Record<GameApiErrorKind, string> = {
  'game-not-found': 'This game could not be found.',
  'placement-out-of-board': 'That position would leave the board.',
  'placement-overlap': 'That position would overlap another ship.',
  'player-ready': 'You are ready, so your fleet can no longer be moved.',
  'game-already-started': 'The game has already started.',
  'fleet-incomplete': 'Place all five ships before you declare ready.',
  'game-update-conflict': 'The game changed at the same moment. Please try again.',
  network: 'The server could not be reached. Check your connection.',
  unexpected: 'Something went wrong. Please try again.',
};

/** The message a player sees for an error. */
export function gameApiErrorMessage(error: unknown): string {
  return MESSAGES[isGameApiError(error) ? error.kind : 'unexpected'];
}
