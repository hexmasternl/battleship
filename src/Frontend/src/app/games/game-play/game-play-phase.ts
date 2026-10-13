import { Component, computed, inject, linkedSignal } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { GamePageStore } from '../game-page/game-page.store';
import { FleetGrid } from './fleet-grid';
import { TargetGrid } from './target-grid';

/** The index of the "You" tab (the opponent's waters) and the "Opponent" tab (the caller's). */
export const YOU_TAB = 0;
export const OPPONENT_TAB = 1;

/**
 * The game play phase: whose turn it is, and two tabs with the two boards. Reads the page's
 * `GamePageStore` and hands its slices to the grids; a selected cell goes back to the store.
 *
 * The tabs follow the turn: "You" is selected when the caller has the turn and "Opponent" when
 * the opponent has it, on first render and on every turn change. In between the player may switch
 * by hand; the selection is the only state this component owns.
 *
 * Mobile first: the turn line, the tabs and the grid stacked.
 */
@Component({
  selector: 'battle-game-play-phase',
  imports: [FleetGrid, MatTabsModule, TargetGrid],
  templateUrl: './game-play-phase.html',
  styleUrl: './game-play-phase.scss',
})
export class GamePlayPhase {
  protected readonly store = inject(GamePageStore);

  /** The selected tab: follows the turn, and holds a manual choice until the turn moves. */
  protected readonly selectedTab = linkedSignal<number>(() =>
    this.store.myTurn() ? YOU_TAB : OPPONENT_TAB,
  );

  protected readonly turnText = computed(() =>
    this.store.myTurn() ? 'Your turn' : "Opponent's turn",
  );
}
