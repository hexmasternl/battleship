import { Component, inject } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogRef,
  MatDialogState,
  MatDialogTitle,
} from '@angular/material/dialog';
import { GameOutcome } from '../game-page/game-page.store';

/** What the result dialog is opened with: how the game ended for the viewing player. */
export interface GameResultData {
  readonly outcome: GameOutcome;
}

/** The only result the dialog closes with, by Close or by Escape. */
export const GAME_RESULT_CLOSED = 'closed';
export type GameResultClosed = typeof GAME_RESULT_CLOSED;

/** The title and message per outcome. */
export const GAME_RESULT_TEXTS: Record<GameOutcome, { title: string; message: string }> = {
  won: { title: 'You won', message: 'The enemy fleet is sunk. Well fought, captain.' },
  lost: { title: 'You lost', message: 'Your fleet is sunk. The enemy holds the sea.' },
  neutral: { title: 'The game has ended', message: 'This battle is over.' },
};

/**
 * Tells the player how the game ended: "You won", "You lost" or, without a winner, "The game has
 * ended". Modal, with one "Close" action that has the initial focus.
 *
 * A click on the backdrop does not close it; Close and Escape do. Material reports a backdrop
 * click and Escape alike (both close without a result), so the dialog is opened to close only
 * with `GAME_RESULT_CLOSED` (see `openGameResultDialog`) and closes itself with it on Escape.
 */
@Component({
  selector: 'battle-game-result-dialog',
  imports: [MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle],
  template: `
    <h2 mat-dialog-title class="game-result-dialog__title" [class.is-won]="outcome === 'won'">
      {{ text.title }}
    </h2>
    <mat-dialog-content>
      <p class="game-result-dialog__message">{{ text.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button
        class="bs-btn bs-btn--primary"
        type="button"
        cdkFocusInitial
        [mat-dialog-close]="closed"
      >
        Close
      </button>
    </mat-dialog-actions>
  `,
  styleUrl: './game-result-dialog.scss',
  host: { '(document:keydown.escape)': 'dismiss()' },
})
export class GameResultDialog {
  readonly #ref = inject<MatDialogRef<GameResultDialog, GameResultClosed>>(MatDialogRef);

  // Dialog data is fixed for the dialog's life, so plain readonly fields are enough here.
  protected readonly outcome = inject<GameResultData>(MAT_DIALOG_DATA).outcome;
  protected readonly text = GAME_RESULT_TEXTS[this.outcome];
  protected readonly closed = GAME_RESULT_CLOSED;

  /** Escape closes the dialog, as Close does. */
  protected dismiss(): void {
    if (this.#ref.getState() === MatDialogState.OPEN) this.#ref.close(GAME_RESULT_CLOSED);
  }
}

/**
 * Opens the result dialog for `outcome`. It closes only with `GAME_RESULT_CLOSED` (Close or
 * Escape), so a click on the backdrop leaves it open; `disableClose` stays off.
 */
export function openGameResultDialog(
  dialog: MatDialog,
  outcome: GameOutcome,
): MatDialogRef<GameResultDialog, GameResultClosed> {
  return dialog.open<GameResultDialog, GameResultData, GameResultClosed>(GameResultDialog, {
    data: { outcome },
    // The Close button, directly: 'first-tabbable' would honour `cdkFocusInitial` too, but only
    // for an element the browser lays out, and a selector does not depend on layout.
    autoFocus: '[cdkFocusInitial]',
    closePredicate: (result) => result === GAME_RESULT_CLOSED,
  });
}
