// Test data shared by the spec files; not imported by application code.
import { GameView, OpponentView, PlayerView, ShipView } from '../app/games/game-view';

export const GAME_CODE = 'K7Q2M9XA';
export const ME_ID = 'player-a';
export const OPPONENT_ID = 'player-b';

export function ship(
  kind: ShipView['kind'],
  column: number,
  row: number,
  orientation: ShipView['orientation'] = 'horizontal',
): ShipView {
  return { kind, column, row, orientation };
}

/** A full fleet that fits: every ship horizontal on its own row. */
export const FULL_FLEET: readonly ShipView[] = [
  ship('carrier', 0, 0),
  ship('battleship', 0, 2),
  ship('cruiser', 0, 4),
  ship('submarine', 0, 6),
  ship('destroyer', 0, 8),
];

export function me(overrides: Partial<PlayerView> = {}): PlayerView {
  return {
    playerId: ME_ID,
    playerName: 'Ann',
    ready: false,
    hasTurn: false,
    ships: [],
    shots: [],
    ...overrides,
  };
}

export function opponent(overrides: Partial<OpponentView> = {}): OpponentView {
  return {
    playerId: OPPONENT_ID,
    playerName: 'Bob',
    ready: false,
    hasTurn: false,
    shots: [],
    ...overrides,
  };
}

/** A `New` game in which the caller is alone and has placed nothing, unless overridden. */
export function gameView(overrides: Partial<GameView> = {}): GameView {
  return {
    gameId: 'game-1',
    gameCode: GAME_CODE,
    state: 'new',
    me: me(),
    opponent: null,
    ...overrides,
  };
}
