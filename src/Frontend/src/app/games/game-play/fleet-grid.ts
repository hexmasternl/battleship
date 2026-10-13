import { Component, computed, input } from '@angular/core';
import { ShipKind, ShipView, ShotView } from '../game-view';
import { gridArea } from '../placement/placement-board';
import { cellLabel, cellsOf, nameOf, rowLabel } from '../placement/placement-rules';
import { BOARD_INDEXES, COLUMN_LABELS, cellKey, shotsByCell, shotWord } from './play-board';
import { ShotMarker } from './shot-marker';

/**
 * The "Opponent" board: the caller's own waters with their ships and the shots the opponent fired
 * at them. Read only: a table whose cells name their coordinate, the ship on them and the shot
 * ("A1, carrier, hit", "D9, miss", "E7").
 */
@Component({
  selector: 'battle-fleet-grid',
  imports: [ShotMarker],
  templateUrl: './fleet-grid.html',
  styleUrl: './fleet-grid.scss',
})
export class FleetGrid {
  /** The caller's ships. */
  readonly ships = input.required<readonly ShipView[]>();
  /** The shots the opponent fired at the caller's board. */
  readonly shots = input.required<readonly ShotView[]>();

  protected readonly columns = COLUMN_LABELS;

  readonly #occupants = computed(() => {
    const occupants = new Map<string, ShipKind>();
    for (const ship of this.ships()) {
      for (const cell of cellsOf(ship)) occupants.set(cellKey(cell), ship.kind);
    }
    return occupants;
  });

  protected readonly rows = computed(() => {
    const shots = shotsByCell(this.shots());
    const occupants = this.#occupants();
    return BOARD_INDEXES.map((row) => ({
      label: rowLabel(row),
      cells: BOARD_INDEXES.map((column) => {
        const cell = { column, row };
        const key = cellKey(cell);
        const shot = shots.get(key);
        const occupant = occupants.get(key);
        const ship = occupant === undefined ? null : nameOf(occupant).toLowerCase();
        const words = [cellLabel(cell), ship, shotWord(shot)];
        return {
          key,
          shot: shot ?? null,
          label: words.filter((word) => word !== null).join(', '),
        };
      }),
    }));
  });

  protected readonly placedShips = computed(() =>
    this.ships().map((ship) => ({
      kind: ship.kind,
      area: gridArea(ship),
      vertical: ship.orientation === 'vertical',
    })),
  );
}
