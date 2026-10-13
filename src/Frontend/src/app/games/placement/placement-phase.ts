import { Component, inject } from '@angular/core';
import { GamePageStore } from '../game-page/game-page.store';
import { PlacementBoard } from './placement-board';
import { ShipList } from './ship-list';

/**
 * The placement phase of a game: the board, the ships still to place and the players. Reads the
 * page's `GamePageStore` and hands its slices to presentational children.
 *
 * Mobile first: one column. From the `md` breakpoint the board spans both rows on the left with
 * the players top right and the ship list bottom right.
 */
@Component({
  selector: 'battle-placement-phase',
  imports: [PlacementBoard, ShipList],
  templateUrl: './placement-phase.html',
  styleUrl: './placement-phase.scss',
})
export class PlacementPhase {
  protected readonly store = inject(GamePageStore);
}
