import { Component, ElementRef, inject, viewChild } from '@angular/core';
import {
  MatDialog,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatProgressBar } from '@angular/material/progress-bar';
import { firstValueFrom } from 'rxjs';
import { JoinGameStore } from './join-game-dialog.store';

/** What the dialog closes with once the player has a seat in the game. */
export interface JoinGameResult {
  readonly gameCode: string;
}

/** The element that gets focus when the dialog opens. */
const CODE_INPUT_SELECTOR = '.join-game-dialog__code';

let nextId = 0;

/**
 * Asks the player for a game code and joins that game. Closes with `{ gameCode }` on success;
 * cancel and Escape close without a result. A refusal (unknown code, full, started) or a failure
 * keeps the dialog open with the reason and the entered code. While the request is in flight the
 * dialog cannot be dismissed and the join action is disabled.
 */
@Component({
  selector: 'battle-join-game-dialog',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatDialogClose, MatProgressBar],
  providers: [JoinGameStore],
  templateUrl: './join-game-dialog.html',
  styleUrl: './join-game-dialog.scss',
})
export class JoinGameDialog {
  protected readonly store = inject(JoinGameStore);
  readonly #ref = inject<MatDialogRef<JoinGameDialog, JoinGameResult>>(MatDialogRef);
  private readonly codeInput = viewChild.required<ElementRef<HTMLInputElement>>('codeInput');

  protected readonly inputId = `battle-join-game-code-${nextId++}`;
  protected readonly introId = `${this.inputId}-intro`;
  protected readonly errorId = `${this.inputId}-error`;

  /** Normalizes what was typed and writes it back, so the input always shows the stored code. */
  protected edit(input: HTMLInputElement): void {
    this.store.edit(input.value);
    const code = this.store.code();
    if (input.value !== code) input.value = code;
  }

  protected async join(): Promise<void> {
    if (!this.store.canJoin()) return;
    // Escape and the backdrop must not drop a request in flight: the seat would be taken unseen.
    this.#ref.disableClose = true;
    const joined = await this.store.join();
    this.#ref.disableClose = false;
    if (joined !== null) {
      this.#ref.close({ gameCode: joined.gameCode });
    } else {
      // Back to the kept code, so it can be corrected or sent again.
      this.codeInput().nativeElement.focus();
    }
  }
}

/**
 * Opens the join-game dialog and resolves the joined game, or `null` when the player cancelled.
 * Focus starts in the game code input and goes back to `opener` when the dialog closes.
 */
export async function openJoinGameDialog(
  dialog: MatDialog,
  opener?: HTMLElement,
): Promise<JoinGameResult | null> {
  const ref = dialog.open<JoinGameDialog, void, JoinGameResult>(JoinGameDialog, {
    // A selector, not 'first-tabbable': the code input is focused directly, whatever the layout.
    autoFocus: CODE_INPUT_SELECTOR,
    restoreFocus: opener ?? true,
  });
  // Escape gives `undefined` and the bare `mat-dialog-close` an empty string: both mean cancelled.
  const closed: unknown = await firstValueFrom(ref.afterClosed());
  return isJoinGameResult(closed) ? closed : null;
}

function isJoinGameResult(value: unknown): value is JoinGameResult {
  return typeof value === 'object' && value !== null && 'gameCode' in value;
}
