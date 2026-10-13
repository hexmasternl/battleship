// What the two game play grids share: the board indexes, the column letters and how a cell names
// its shot. The board recipe itself (`.bs-board`, `.bs-cell`) is global, see styles/_game.scss.
import { ShotView } from '../game-view';
import { BOARD_SIZE, Cell, columnLabel } from '../placement/placement-rules';

/** 0 to 11: the column and row indexes of the board. */
export const BOARD_INDEXES: readonly number[] = Array.from({ length: BOARD_SIZE }, (_, i) => i);

/** The column letters A to L, in board order. */
export const COLUMN_LABELS: readonly string[] = BOARD_INDEXES.map(columnLabel);

/** A stable key of a cell, such as `4-6`. */
export function cellKey(cell: Cell): string {
  return `${cell.column}-${cell.row}`;
}

/** The shots by cell key, for looking a cell up while the grid is built. */
export function shotsByCell(shots: readonly ShotView[]): ReadonlyMap<string, ShotView> {
  return new Map(shots.map((shot) => [cellKey(shot), shot]));
}

/** How a cell's label names its shot: `hit`, `miss`, or nothing for a cell not fired at. */
export function shotWord(shot: ShotView | undefined): string | null {
  if (shot === undefined) return null;
  return shot.hit ? 'hit' : 'miss';
}
