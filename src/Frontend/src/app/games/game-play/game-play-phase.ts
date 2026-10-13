import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  untracked,
} from '@angular/core';
import { MatSnackBar, MatSnackBarRef } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { GamePageStore } from '../game-page/game-page.store';
import { fireShotErrorMessage } from '../games-api';
import { FleetGrid } from './fleet-grid';
import { ShotConfirmData, ShotConfirmSnackbar } from './shot-confirm-snackbar';
import { TargetGrid } from './target-grid';

/** The index of the "You" tab (the opponent's waters) and the "Opponent" tab (the caller's). */
export const YOU_TAB = 0;
export const OPPONENT_TAB = 1;

/** How long a refused or failed shot's message stays, in milliseconds. */
export const SHOT_ERROR_DURATION = 5000;

/**
 * The game play phase: whose turn it is, and two tabs with the two boards. Reads the page's
 * `GamePageStore` and hands its slices to the grids; a selected cell goes back to the store.
 *
 * The tabs follow the turn: "You" is selected when the caller has the turn and "Opponent" when
 * the opponent has it, on first render and on every turn change. In between the player may switch
 * by hand; the selection is the only state this component owns.
 *
 * While a target is selected a snackbar asks to confirm the shot (no auto-dismiss); it follows the
 * target and closes when the target is cleared. A refused or failed shot shows its message in a
 * snackbar of its own that dismisses itself.
 *
 * With `readonly` (a finished game) it shows the final boards only: no turn line, no selectable
 * cell, no confirm snackbar, and the selected tab stays put.
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
  readonly #snackBar = inject(MatSnackBar);

  /**
   * The final boards of a finished game: no cell can be selected, no shot confirmed, nobody is
   * shown with the turn and the tabs no longer follow the turn.
   */
  readonly readonly = input(false);

  /**
   * The selected tab: follows the turn, and holds a manual choice until the turn moves. Read only,
   * it stays where it was; shown read only from the start, it opens on the board that decided the
   * game (the caller's own fleet for a loss, the opponent's waters otherwise).
   */
  protected readonly selectedTab = linkedSignal<
    { readonly myTurn: boolean; readonly readonly: boolean; readonly lost: boolean },
    number
  >({
    source: () => ({
      myTurn: this.store.myTurn(),
      readonly: this.readonly(),
      lost: this.store.outcome() === 'lost',
    }),
    computation: (source, previous) => {
      if (source.readonly) return previous?.value ?? (source.lost ? OPPONENT_TAB : YOU_TAB);
      return source.myTurn ? YOU_TAB : OPPONENT_TAB;
    },
  });

  /** Whether a cell of the opponent's waters may be selected: never on the final boards. */
  protected readonly canShoot = computed(() => !this.readonly() && this.store.canShoot());

  protected readonly turnText = computed(() =>
    this.store.myTurn() ? 'Your turn' : "Opponent's turn",
  );

  /** The open confirm snackbar. Not state: a handle on the overlay, nothing renders it. */
  #confirm: MatSnackBarRef<ShotConfirmSnackbar> | null = null;

  readonly #confirmData: ShotConfirmData = {
    // Keeps the last name while the snackbar closes, so it never reads "Fire at ?".
    cell: linkedSignal<string | null, string>({
      source: this.store.targetName,
      computation: (name, previous) => name ?? previous?.value ?? '',
    }),
    firing: computed(() => this.store.shotStatus() === 'firing'),
    shoot: () => void this.store.fireAtTarget(),
    cancel: () => this.store.clearTarget(),
  };

  constructor() {
    // Leaves the reactive graph: the snackbar overlay opens with a target and closes without one.
    effect(() => {
      const hasTarget = this.store.target() !== null && !this.readonly();
      untracked(() => (hasTarget ? this.#openConfirm() : this.#closeConfirm()));
    });

    // Leaves the reactive graph: a refused or failed shot is told in a snackbar.
    effect(() => {
      const error = this.store.shotError();
      if (error === null) return;
      untracked(() =>
        this.#snackBar.open(fireShotErrorMessage(error), 'Dismiss', {
          duration: SHOT_ERROR_DURATION,
        }),
      );
    });

    inject(DestroyRef).onDestroy(() => this.#closeConfirm());
  }

  /**
   * Opens the confirm snackbar unless it is open; a moved target only changes its label. An error
   * snackbar never replaces it unnoticed: the store clears the target with every shot error.
   */
  #openConfirm(): void {
    if (this.#confirm !== null) return;
    this.#confirm = this.#snackBar.openFromComponent(ShotConfirmSnackbar, {
      data: this.#confirmData,
      politeness: 'polite',
    });
  }

  #closeConfirm(): void {
    this.#confirm?.dismiss();
    this.#confirm = null;
  }
}
