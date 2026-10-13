import { Component, inject } from '@angular/core';
import {
  MatDialog,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { PlayerName } from './player-name';

/**
 * Hosts `battle-player-name` in a modal dialog. Closes with `true` once the player is created and
 * with `false` from the cancel action; Escape closes without a result, which `openPlayerNameDialog`
 * reports as `false` too.
 */
@Component({
  selector: 'battle-player-name-dialog',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatDialogClose, PlayerName],
  template: `
    <h2 mat-dialog-title class="player-name-dialog__title">Choose your player name</h2>
    <mat-dialog-content>
      <p class="player-name-dialog__intro">
        Other captains will see this name when you meet them at sea.
      </p>
      <battle-player-name (playerCreated)="created()" />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button class="bs-btn bs-btn--ghost" type="button" [mat-dialog-close]="false">Cancel</button>
    </mat-dialog-actions>
  `,
  styleUrl: './player-name-dialog.scss',
})
export class PlayerNameDialog {
  readonly #ref = inject<MatDialogRef<PlayerNameDialog, boolean>>(MatDialogRef);

  protected created(): void {
    this.#ref.close(true);
  }
}

/**
 * Opens the player-name dialog and resolves `true` only when it closed because the player was
 * created; cancel, Escape and a backdrop click resolve `false`. Focus goes back to `opener`.
 */
export async function openPlayerNameDialog(
  dialog: MatDialog,
  opener?: HTMLElement,
): Promise<boolean> {
  const ref = dialog.open<PlayerNameDialog, void, boolean>(PlayerNameDialog, {
    autoFocus: 'first-tabbable',
    restoreFocus: opener ?? true,
  });
  return (await firstValueFrom(ref.afterClosed())) === true;
}
