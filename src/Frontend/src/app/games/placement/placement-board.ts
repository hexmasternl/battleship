import { Component, ElementRef, computed, inject, input, output, signal } from '@angular/core';
import { ShipKind, ShipView } from '../game-view';
import {
  BOARD_SIZE,
  Cell,
  cellLabel,
  cellsOf,
  columnLabel,
  nameOf,
  rowLabel,
} from './placement-rules';

/** A ship shown while it is dragged over the board, and whether it may be dropped there. */
export interface PlacementPreview {
  readonly ship: ShipView;
  readonly valid: boolean;
}

const INDEXES = Array.from({ length: BOARD_SIZE }, (_, i) => i);
const key = (cell: Cell) => `${cell.column}-${cell.row}`;

/** The grid lines a ship covers, clipped to the board; the tracks start after the header. */
function gridArea(ship: ShipView): { column: string; row: string } {
  const cells = cellsOf(ship).filter(
    (c) => c.column >= 0 && c.column < BOARD_SIZE && c.row >= 0 && c.row < BOARD_SIZE,
  );
  const first = cells[0] ?? { column: 0, row: 0 };
  const span = Math.max(cells.length, 1);
  return ship.orientation === 'horizontal'
    ? { column: `${first.column + 2} / span ${span}`, row: `${first.row + 2}` }
    : { column: `${first.column + 2}`, row: `${first.row + 2} / span ${span}` };
}

/**
 * The caller's board during placement: a 12 x 12 `.bs-board` with A-L / 1-12 labels and the placed
 * ships on top. Presentational: it shows what it is given and reports what the player does.
 *
 * The cells form an ARIA grid with a roving tab stop: one cell is in the tab order, the arrow keys,
 * Home and End move between cells, and Enter or Space activates the focused cell. Every cell names
 * its coordinate and what lies on it ("C4, empty", "C4, cruiser").
 */
@Component({
  selector: 'battle-placement-board',
  templateUrl: './placement-board.html',
  styleUrl: './placement-board.scss',
  host: { '(keydown.escape)': 'cleared.emit()' },
})
export class PlacementBoard {
  readonly #host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  readonly ships = input.required<readonly ShipView[]>();
  readonly activeKind = input<ShipKind | null>(null);
  readonly preview = input<PlacementPreview | null>(null);
  /** After ready: nothing can be selected, moved or placed. */
  readonly readOnly = input(false);

  /** A cell was activated (click, tap, Enter or Space). */
  readonly cellActivated = output<Cell>();
  /** A placed ship was activated. */
  readonly shipActivated = output<ShipKind>();
  /** Escape was pressed: the active ship is to be cleared. */
  readonly cleared = output<void>();

  /** The cell that holds the roving tab stop. UI state of this component only. */
  protected readonly focused = signal<Cell>({ column: 0, row: 0 });

  protected readonly columns = INDEXES.map((column) => columnLabel(column));

  readonly #occupants = computed(() => {
    const occupants = new Map<string, ShipKind>();
    for (const ship of this.ships()) {
      for (const cell of cellsOf(ship)) occupants.set(key(cell), ship.kind);
    }
    return occupants;
  });

  protected readonly rows = computed(() => {
    const occupants = this.#occupants();
    const focused = key(this.focused());
    return INDEXES.map((row) => ({
      label: rowLabel(row),
      cells: INDEXES.map((column) => {
        const cell = { column, row };
        const occupant = occupants.get(key(cell));
        return {
          cell,
          key: key(cell),
          tabStop: key(cell) === focused,
          label: `${cellLabel(cell)}, ${occupant === undefined ? 'empty' : nameOf(occupant).toLowerCase()}`,
        };
      }),
    }));
  });

  protected readonly placedShips = computed(() =>
    this.ships().map((ship) => {
      const cells = cellsOf(ship);
      return {
        kind: ship.kind,
        ship,
        area: gridArea(ship),
        vertical: ship.orientation === 'vertical',
        active: ship.kind === this.activeKind(),
        label: `${nameOf(ship.kind)}, ${cellLabel(cells[0])} to ${cellLabel(cells[cells.length - 1])}, ${ship.orientation}`,
      };
    }),
  );

  protected readonly previewShip = computed(() => {
    const preview = this.preview();
    if (preview === null) return null;
    return {
      area: gridArea(preview.ship),
      vertical: preview.ship.orientation === 'vertical',
      valid: preview.valid,
    };
  });

  /** The cell under a viewport point, or `null` when the point is not over a cell. */
  cellAt(point: { readonly x: number; readonly y: number }): Cell | null {
    const first = this.#cellElement({ column: 0, row: 0 })?.getBoundingClientRect();
    const last = this.#cellElement({
      column: BOARD_SIZE - 1,
      row: BOARD_SIZE - 1,
    })?.getBoundingClientRect();
    if (first === undefined || last === undefined) return null;
    return cellFromPoint(point, first, last);
  }

  protected activate(cell: Cell): void {
    this.focused.set(cell);
    if (!this.readOnly()) this.cellActivated.emit(cell);
  }

  protected activateShip(kind: ShipKind): void {
    if (!this.readOnly()) this.shipActivated.emit(kind);
  }

  protected move(event: KeyboardEvent, cell: Cell): void {
    const next = neighbour(cell, event.key, event.ctrlKey);
    if (next === null) return;
    event.preventDefault();
    this.focused.set(next);
    this.#cellElement(next)?.focus();
  }

  #cellElement(cell: Cell): HTMLElement | null {
    return this.#host.querySelector<HTMLElement>(`[data-cell="${key(cell)}"]`);
  }
}

/** The cell a key moves the focus to from `cell`, or `null` for keys that do not move it. */
export function neighbour(cell: Cell, keyName: string, ctrl = false): Cell | null {
  const clamp = (n: number) => Math.min(Math.max(n, 0), BOARD_SIZE - 1);
  switch (keyName) {
    case 'ArrowLeft':
      return { ...cell, column: clamp(cell.column - 1) };
    case 'ArrowRight':
      return { ...cell, column: clamp(cell.column + 1) };
    case 'ArrowUp':
      return { ...cell, row: clamp(cell.row - 1) };
    case 'ArrowDown':
      return { ...cell, row: clamp(cell.row + 1) };
    case 'Home':
      return ctrl ? { column: 0, row: 0 } : { ...cell, column: 0 };
    case 'End':
      return ctrl
        ? { column: BOARD_SIZE - 1, row: BOARD_SIZE - 1 }
        : { ...cell, column: BOARD_SIZE - 1 };
    default:
      return null;
  }
}

/**
 * The cell under a point, given the rectangles of the first (A1) and last (L12) cells. The pitch
 * is measured from the rendered board, so it follows the responsive layout and the grid gaps.
 */
export function cellFromPoint(
  point: { readonly x: number; readonly y: number },
  first: Pick<DOMRect, 'left' | 'top'>,
  last: Pick<DOMRect, 'left' | 'top' | 'right' | 'bottom'>,
): Cell | null {
  if (point.x < first.left || point.y < first.top || point.x > last.right || point.y > last.bottom)
    return null;
  const pitchX = (last.left - first.left) / (BOARD_SIZE - 1);
  const pitchY = (last.top - first.top) / (BOARD_SIZE - 1);
  if (pitchX <= 0 || pitchY <= 0) return null;
  const clamp = (n: number) => Math.min(Math.max(n, 0), BOARD_SIZE - 1);
  return {
    column: clamp(Math.floor((point.x - first.left) / pitchX)),
    row: clamp(Math.floor((point.y - first.top) / pitchY)),
  };
}
