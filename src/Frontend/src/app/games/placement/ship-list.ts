import { CdkDrag, CdkDropList } from '@angular/cdk/drag-drop';
import { Component, input, output } from '@angular/core';
import { ShipKind } from '../game-view';
import { Point, ShipDragStart, grabIndexAt } from './placement-drag';
import { FleetShip } from './placement-rules';

/**
 * The ships not yet on the board, each with its name and drawn at its length so the sizes read
 * apart at a glance. Presentational: selecting and dragging a ship are reported to the parent,
 * which works out where a drag ends on the board. A ship from the list is always horizontal.
 */
@Component({
  selector: 'battle-ship-list',
  imports: [CdkDrag, CdkDropList],
  templateUrl: './ship-list.html',
  styleUrl: './ship-list.scss',
})
export class ShipList {
  readonly ships = input.required<readonly FleetShip[]>();
  readonly activeKind = input<ShipKind | null>(null);
  readonly readOnly = input(false);

  /** A ship was selected (click, tap, Enter or Space). */
  readonly shipActivated = output<ShipKind>();
  /** A ship started to be dragged. */
  readonly shipDragStarted = output<ShipDragStart>();
  /** The pointer moved during a drag (viewport coordinates). */
  readonly dragMoved = output<Point>();
  /** A drag ended. */
  readonly dragEnded = output<void>();

  /** The hull cell the pointer went down on; see `PlacementBoard`. Not rendered. */
  #grabIndex = 0;

  protected activate(kind: ShipKind): void {
    if (!this.readOnly()) this.shipActivated.emit(kind);
  }

  protected grabbed(event: PointerEvent, ship: FleetShip): void {
    const hull = (event.currentTarget as HTMLElement).querySelector('.ship-list__hull');
    this.#grabIndex =
      hull === null
        ? 0
        : grabIndexAt(
            { x: event.clientX, y: event.clientY },
            hull.getBoundingClientRect(),
            ship.length,
            'horizontal',
          );
  }

  protected dragStarted(ship: FleetShip): void {
    this.shipDragStarted.emit({
      kind: ship.kind,
      orientation: 'horizontal',
      grabIndex: this.#grabIndex,
    });
  }
}
