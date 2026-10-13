import { Component, inject } from '@angular/core';
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
import { CreateGameStore } from './create-game-dialog.store';

/** What the dialog closes with once the game exists. */
export interface CreateGameResult {
  readonly gameCode: string;
}

/**
 * Asks the player to confirm creating a new game and creates it. Closes with `{ gameCode }` on
 * success; cancel and Escape close without a result. A failure keeps the dialog open with a
 * retryable error. While the request is in flight the dialog cannot be dismissed and the confirm
 * action is disabled.
 */
@Component({
  selector: 'battle-create-game-dialog',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatDialogClose, MatProgressBar],
  providers: [CreateGameStore],
  templateUrl: './create-game-dialog.html',
  styleUrl: './create-game-dialog.scss',
})
export class CreateGameDialog {
  protected readonly store = inject(CreateGameStore);
  readonly #ref = inject<MatDialogRef<CreateGameDialog, CreateGameResult>>(MatDialogRef);

  protected async confirm(): Promise<void> {
    if (this.store.isCreating()) return;
    // Escape and the backdrop must not drop a request in flight: the game would exist unseen.
    this.#ref.disableClose = true;
    const created = await this.store.create();
    this.#ref.disableClose = false;
    if (created !== null) this.#ref.close({ gameCode: created.gameCode });
  }
}

/**
 * Opens the create-game dialog and resolves the created game, or `null` when the player cancelled.
 * Focus goes back to `opener` when the dialog closes.
 */
export async function openCreateGameDialog(
  dialog: MatDialog,
  opener?: HTMLElement,
): Promise<CreateGameResult | null> {
  const ref = dialog.open<CreateGameDialog, void, CreateGameResult>(CreateGameDialog, {
    autoFocus: 'first-tabbable',
    restoreFocus: opener ?? true,
  });
  // Escape gives `undefined` and the bare `mat-dialog-close` an empty string: both mean cancelled.
  const closed: unknown = await firstValueFrom(ref.afterClosed());
  return isCreateGameResult(closed) ? closed : null;
}

function isCreateGameResult(value: unknown): value is CreateGameResult {
  return typeof value === 'object' && value !== null && 'gameCode' in value;
}
