import { Component, effect, inject, input, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { GamePlayPhase } from '../game-play/game-play-phase';
import { openGameResultDialog } from '../game-result-dialog/game-result-dialog';
import { PlacementPhase } from '../placement/placement-phase';
import { GameOutcome, GamePageStore } from './game-page.store';

/**
 * The page of one game at `/games/:gameCode` (the route parameter binds to `gameCode`). Loads the
 * caller's view of the game and shows the phase that matches its state; everything it shows comes
 * from `GamePageStore`, so a refresh shows the same page.
 *
 * Once the game is over the final boards stay, read only, and the result dialog opens over them,
 * once per page however the end arrived (event, own shot, load). Closing it leads home.
 */
@Component({
  selector: 'battle-game-page',
  imports: [GamePlayPhase, MatButtonModule, MatProgressSpinnerModule, PlacementPhase, RouterLink],
  providers: [GamePageStore],
  templateUrl: './game-page.html',
  styleUrl: './game-page.scss',
})
export class GamePage {
  protected readonly store = inject(GamePageStore);
  readonly #dialog = inject(MatDialog);
  readonly #router = inject(Router);
  readonly gameCode = input.required<string>();

  /** Whether the result dialog was opened. Not state: nothing renders it, it only guards the effect. */
  #resultShown = false;

  constructor() {
    // Leaves the reactive graph: a (new) code in the URL starts a request for that game.
    effect(() => {
      const code = this.gameCode();
      untracked(() => void this.store.load(code));
    });

    // Leaves the reactive graph: the end of the game opens the result dialog, once per page.
    effect(() => {
      const outcome = this.store.outcome();
      if (outcome === null || this.#resultShown) return;
      this.#resultShown = true;
      untracked(() => void this.#showResult(outcome));
    });
  }

  /** Shows the result; however it is closed (Close, Escape), the player goes home. */
  async #showResult(outcome: GameOutcome): Promise<void> {
    const ref = openGameResultDialog(this.#dialog, outcome);
    await firstValueFrom(ref.afterClosed());
    await this.#router.navigateByUrl('/');
  }
}
