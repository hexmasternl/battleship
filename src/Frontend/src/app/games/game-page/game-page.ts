import { Component, effect, inject, input, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { PlacementPhase } from '../placement/placement-phase';
import { GamePageStore } from './game-page.store';

/**
 * The page of one game at `/games/:gameCode` (the route parameter binds to `gameCode`). Loads the
 * caller's view of the game and shows the phase that matches its state; everything it shows comes
 * from `GamePageStore`, so a refresh shows the same page.
 */
@Component({
  selector: 'battle-game-page',
  imports: [MatButtonModule, MatProgressSpinnerModule, PlacementPhase, RouterLink],
  providers: [GamePageStore],
  templateUrl: './game-page.html',
  styleUrl: './game-page.scss',
})
export class GamePage {
  protected readonly store = inject(GamePageStore);
  readonly gameCode = input.required<string>();

  constructor() {
    // Leaves the reactive graph: a (new) code in the URL starts a request for that game.
    effect(() => {
      const code = this.gameCode();
      untracked(() => void this.store.load(code));
    });
  }
}
