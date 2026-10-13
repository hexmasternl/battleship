// The board and fleet rules of the placement phase, mirroring the Games domain (`Fleet`, `Board`,
// `ShipPlacement.Cells` and the checks of `Game.PlaceShip`). They give the player instant feedback;
// the server stays authoritative and every change is checked again there.
import { Orientation, ShipKind, ShipView } from '../game-view';

/** The board is `BOARD_SIZE` columns (A to L) by `BOARD_SIZE` rows (1 to 12). */
export const BOARD_SIZE = 12;

/** One ship type of the fleet. */
export interface FleetShip {
  readonly kind: ShipKind;
  readonly name: string;
  readonly length: number;
}

/** The classic fleet, in the order the ship list shows it. */
export const FLEET: readonly FleetShip[] = [
  { kind: 'carrier', name: 'Carrier', length: 5 },
  { kind: 'battleship', name: 'Battleship', length: 4 },
  { kind: 'cruiser', name: 'Cruiser', length: 3 },
  { kind: 'submarine', name: 'Submarine', length: 3 },
  { kind: 'destroyer', name: 'Destroyer', length: 2 },
];

/** A square of the board: zero-based column and row. */
export interface Cell {
  readonly column: number;
  readonly row: number;
}

/** Why a placement is refused, or `ok`. */
export type PlacementVerdict =
  | { readonly ok: true }
  | { readonly ok: false; readonly reason: 'out-of-board' }
  | { readonly ok: false; readonly reason: 'overlap'; readonly other: ShipKind };

function fleetShip(kind: ShipKind): FleetShip {
  const found = FLEET.find((ship) => ship.kind === kind);
  if (found === undefined) throw new Error(`Unknown ship kind "${kind}"`);
  return found;
}

export function lengthOf(kind: ShipKind): number {
  return fleetShip(kind).length;
}

export function nameOf(kind: ShipKind): string {
  return fleetShip(kind).name;
}

/** The cells a ship covers, from its origin to the right (horizontal) or downwards (vertical). */
export function cellsOf(ship: ShipView): Cell[] {
  return Array.from({ length: lengthOf(ship.kind) }, (_, i) =>
    ship.orientation === 'horizontal'
      ? { column: ship.column + i, row: ship.row }
      : { column: ship.column, row: ship.row + i },
  );
}

export function isOnBoard(cell: Cell): boolean {
  return cell.column >= 0 && cell.column < BOARD_SIZE && cell.row >= 0 && cell.row < BOARD_SIZE;
}

/**
 * Checks a placement against the board and the caller's other ships, in the order the server
 * checks it: off the board first, then overlap. The ship's own earlier placement is ignored, so
 * a ship may move onto cells it covers now. Touching ships are allowed.
 */
export function checkPlacement(ship: ShipView, fleet: readonly ShipView[]): PlacementVerdict {
  const cells = cellsOf(ship);
  if (!cells.every(isOnBoard)) return { ok: false, reason: 'out-of-board' };
  for (const other of fleet) {
    if (other.kind === ship.kind) continue;
    const taken = cellsOf(other);
    if (cells.some((c) => taken.some((t) => t.column === c.column && t.row === c.row))) {
      return { ok: false, reason: 'overlap', other: other.kind };
    }
  }
  return { ok: true };
}

/** The fleet with `ship` placed: its earlier placement replaced, or added in fleet order. */
export function withPlacement(fleet: readonly ShipView[], ship: ShipView): readonly ShipView[] {
  const others = fleet.filter((placed) => placed.kind !== ship.kind);
  const order = (kind: ShipKind) => FLEET.findIndex((f) => f.kind === kind);
  return [...others, ship].sort((a, b) => order(a.kind) - order(b.kind));
}

/** The other orientation. */
export function turned(orientation: Orientation): Orientation {
  return orientation === 'horizontal' ? 'vertical' : 'horizontal';
}

/** The ship turned 90 degrees clockwise about its origin, which stays where it is. */
export function rotated(ship: ShipView): ShipView {
  return { ...ship, orientation: turned(ship.orientation) };
}

/** The letter of a column: 0 is A, 11 is L. */
export function columnLabel(column: number): string {
  return String.fromCharCode('A'.charCodeAt(0) + column);
}

/** The label of a row: 0 is 1, 11 is 12. */
export function rowLabel(row: number): string {
  return String(row + 1);
}

/** The coordinate of a cell as the board labels it, such as `C4`. */
export function cellLabel(cell: Cell): string {
  return `${columnLabel(cell.column)}${rowLabel(cell.row)}`;
}

/** The message that explains a refused placement or rotation of `kind`. */
export function refusalMessage(kind: ShipKind, verdict: PlacementVerdict): string | null {
  if (verdict.ok) return null;
  const ship = `The ${nameOf(kind).toLowerCase()}`;
  return verdict.reason === 'out-of-board'
    ? `${ship} would leave the board`
    : `${ship} would overlap the ${nameOf(verdict.other).toLowerCase()}`;
}
