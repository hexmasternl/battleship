import { Component } from '@angular/core';

/** The five ships of the fleet and their lengths in cells (design system section 8.2). */
export interface Ship {
  readonly name: string;
  readonly length: number;
}

export const FLEET: readonly Ship[] = [
  { name: 'Carrier', length: 5 },
  { name: 'Battleship', length: 4 },
  { name: 'Destroyer', length: 3 },
  { name: 'Submarine', length: 3 },
  { name: 'Patrol Boat', length: 2 },
];

/** The longest ship, used to draw every ship bar in proportion. */
export const LONGEST_SHIP = Math.max(...FLEET.map((ship) => ship.length));

/**
 * The information section below the hero: four cards explaining how a game works, the fleet,
 * the grid, and how turns and hits are resolved. Static copy, no state.
 */
@Component({
  selector: 'battle-info-section',
  host: { class: 'info' },
  templateUrl: './info-section.html',
  styleUrl: './info-section.scss',
})
export class InfoSection {
  protected readonly fleet = FLEET;
  protected readonly longestShip = LONGEST_SHIP;
}
