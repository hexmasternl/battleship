import { Component, ElementRef, computed, inject, input, output, signal } from '@angular/core';
import { ShotView } from '../game-view';
import { neighbour } from '../placement/placement-board';
import { Cell, cellLabel, rowLabel } from '../placement/placement-rules';
import { BOARD_INDEXES, COLUMN_LABELS, cellKey, shotsByCell, shotWord } from './play-board';
import { ShotMarker } from './shot-marker';

/**
 * The "You" board: the opponent's waters with only the caller's own shots, never the opponent's
 * ships. Presentational: it shows what it is given and reports the cell the player selects.
 *
 * Every cell is a button that names its coordinate and shot ("E7, hit", "E7, miss", "E7"). A cell
 * that may not be fired at (no turn, a shot in flight, already shot) is disabled. The cells form
 * an ARIA grid with a roving tab stop, as on the placement board.
 */
@Component({
  selector: 'battle-target-grid',
  imports: [ShotMarker],
  templateUrl: './target-grid.html',
  styleUrl: './target-grid.scss',
  host: { '(keydown.escape)': 'cleared.emit()' },
})
export class TargetGrid {
  readonly #host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  /** The shots the caller fired. */
  readonly shots = input.required<readonly ShotView[]>();
  /** The selected target, if any. */
  readonly target = input<Cell | null>(null);
  /** Whether the caller may select a target now. */
  readonly canShoot = input(false);
  /** The cells of shots drawn since the board was loaded; their markers animate in. */
  readonly fresh = input<readonly Cell[]>([]);

  /** A cell that may be fired at was activated (click, tap, Enter or Space). */
  readonly cellSelected = output<Cell>();
  /** Escape was pressed: the target is to be cleared. */
  readonly cleared = output<void>();

  /** The cell that holds the roving tab stop. UI state of this component only. */
  protected readonly focused = signal<Cell>({ column: 0, row: 0 });

  protected readonly columns = COLUMN_LABELS;

  protected readonly rows = computed(() => {
    const shots = shotsByCell(this.shots());
    const target = this.target();
    const targetKey = target === null ? null : cellKey(target);
    const focusedKey = cellKey(target ?? this.focused());
    const canShoot = this.canShoot();
    const fresh = new Set(this.fresh().map(cellKey));
    return BOARD_INDEXES.map((row) => ({
      label: rowLabel(row),
      cells: BOARD_INDEXES.map((column) => {
        const cell = { column, row };
        const key = cellKey(cell);
        const shot = shots.get(key);
        const isTarget = key === targetKey;
        const words = [cellLabel(cell), shotWord(shot), isTarget ? 'target' : null];
        return {
          cell,
          key,
          shot: shot ?? null,
          fresh: fresh.has(key),
          target: isTarget,
          selectable: canShoot && shot === undefined,
          tabStop: key === focusedKey,
          label: words.filter((word) => word !== null).join(', '),
        };
      }),
    }));
  });

  protected select(cell: Cell, selectable: boolean): void {
    this.focused.set(cell);
    if (selectable) this.cellSelected.emit(cell);
  }

  protected move(event: KeyboardEvent, cell: Cell): void {
    const next = neighbour(cell, event.key, event.ctrlKey);
    if (next === null) return;
    event.preventDefault();
    this.focused.set(next);
    this.#host.querySelector<HTMLElement>(`[data-cell="${cellKey(next)}"]`)?.focus();
  }
}
