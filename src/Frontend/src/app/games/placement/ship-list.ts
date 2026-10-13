import { Component, input, output } from '@angular/core';
import { ShipKind } from '../game-view';
import { FleetShip } from './placement-rules';

/**
 * The ships not yet on the board, each with its name and drawn at its length so the sizes read
 * apart at a glance. Presentational: selecting a ship is reported to the parent.
 */
@Component({
  selector: 'battle-ship-list',
  templateUrl: './ship-list.html',
  styleUrl: './ship-list.scss',
})
export class ShipList {
  readonly ships = input.required<readonly FleetShip[]>();
  readonly activeKind = input<ShipKind | null>(null);
  readonly readOnly = input(false);

  /** A ship was selected (click, tap, Enter or Space). */
  readonly shipActivated = output<ShipKind>();

  protected activate(kind: ShipKind): void {
    if (!this.readOnly()) this.shipActivated.emit(kind);
  }
}
