/** The five ship kinds of the classic fleet, as the Games API names them (camelCase). */
export type ShipKind = 'carrier' | 'battleship' | 'cruiser' | 'submarine' | 'destroyer';

/** How a ship lies on the board, from its origin to the right or downwards. */
export type Orientation = 'horizontal' | 'vertical';

/** The lifecycle state of a game. */
export type GameState = 'new' | 'started' | 'completed' | 'abandoned';

/** One placed ship: its kind, its origin (zero-based column and row) and its orientation. */
export interface ShipView {
  readonly kind: ShipKind;
  readonly column: number;
  readonly row: number;
  readonly orientation: Orientation;
}

/** Where a ship is to be placed: the body of `PUT /api/games/{code}/ships/{kind}`. */
export interface ShipPlacementRequest {
  readonly column: number;
  readonly row: number;
  readonly orientation: Orientation;
}

/** One shot a player fired. */
export interface ShotView {
  readonly column: number;
  readonly row: number;
  readonly hit: boolean;
}

/** The caller in their own view of the game, including their ships. */
export interface PlayerView {
  readonly playerId: string;
  readonly playerName: string;
  readonly ready: boolean;
  readonly hasTurn: boolean;
  readonly ships: readonly ShipView[];
  readonly shots: readonly ShotView[];
}

/** The other player as the caller may see them: never their ships. */
export interface OpponentView {
  readonly playerId: string;
  readonly playerName: string;
  readonly ready: boolean;
  readonly hasTurn: boolean;
  readonly shots: readonly ShotView[];
}

/** The caller's view of one game, as every game endpoint returns it. */
export interface GameView {
  readonly gameId: string;
  readonly gameCode: string;
  readonly state: GameState;
  readonly me: PlayerView;
  /** `null` while the caller is alone in the game. */
  readonly opponent: OpponentView | null;
}

/** A `GameView` as it arrives on the wire, before its enum casing is normalised. */
export interface GameViewResponse {
  readonly gameId: string;
  readonly gameCode: string;
  readonly state: string;
  readonly me: Omit<PlayerView, 'ships'> & {
    readonly ships: readonly (Omit<ShipView, 'kind' | 'orientation'> & {
      readonly kind: string;
      readonly orientation: string;
    })[];
  };
  readonly opponent: OpponentView | null;
}

/**
 * Reads a `GameView` off the wire. The Games API writes enums as camelCase strings, but the
 * casing is normalised here, in one place, so a `"New"` or `"Carrier"` reads the same.
 */
export function toGameView(response: GameViewResponse): GameView {
  return {
    ...response,
    state: response.state.toLowerCase() as GameState,
    me: {
      ...response.me,
      ships: response.me.ships.map((ship) => ({
        ...ship,
        kind: ship.kind.toLowerCase() as ShipKind,
        orientation: ship.orientation.toLowerCase() as Orientation,
      })),
    },
  };
}
