import { Component, inject, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { GamePageStore } from '../game-page/game-page.store';
import { PlacementBoard } from './placement-board';
import { PlayersPanel } from './players-panel';
import { Point } from './placement-drag';
import { ShipList } from './ship-list';

/**
 * The placement phase of a game: the board, the ships still to place and the players. Reads the
 * page's `GamePageStore` and hands its slices to presentational children; what the player does
 * goes back to the store's intents.
 *
 * A drag from the list or on the board is followed here: the pointer position is turned into a
 * board cell (measured from the rendered board, so it follows the responsive layout) and handed
 * to the store, which previews the ship and places it on a valid drop.
 *
 * Mobile first: one column. From the `md` breakpoint the board spans both rows on the left with
 * the players top right and the ship list bottom right.
 */
@Component({
  selector: 'battle-placement-phase',
  imports: [MatButtonModule, MatIconModule, PlacementBoard, PlayersPanel, ShipList],
  templateUrl: './placement-phase.html',
  styleUrl: './placement-phase.scss',
  host: { '(keydown.escape)': 'store.clearActive()' },
})
export class PlacementPhase {
  protected readonly store = inject(GamePageStore);
  protected readonly board = viewChild.required(PlacementBoard);

  protected dragMoved(point: Point): void {
    this.store.dragOver(this.board().cellAt(point));
  }
}
