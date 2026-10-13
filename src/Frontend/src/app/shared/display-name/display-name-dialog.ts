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
import { DisplayName } from './display-name';

/**
 * Hosts `battle-display-name` in a modal dialog. Closes with `true` once the user is created and
 * with `false` from the cancel action; Escape closes without a result, which `openDisplayNameDialog`
 * reports as `false` too.
 */
@Component({
  selector: 'battle-display-name-dialog',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatDialogClose, DisplayName],
  template: `
    <h2 mat-dialog-title class="display-name-dialog__title">Choose your player name</h2>
    <mat-dialog-content>
      <p class="display-name-dialog__intro">
        Other captains will see this name when you meet them at sea.
      </p>
      <battle-display-name (userCreated)="created()" />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button class="bs-btn bs-btn--ghost" type="button" [mat-dialog-close]="false">Cancel</button>
    </mat-dialog-actions>
  `,
  styleUrl: './display-name-dialog.scss',
})
export class DisplayNameDialog {
  readonly #ref = inject<MatDialogRef<DisplayNameDialog, boolean>>(MatDialogRef);

  protected created(): void {
    this.#ref.close(true);
  }
}

/**
 * Opens the display-name dialog and resolves `true` only when it closed because the user was
 * created; cancel, Escape and a backdrop click resolve `false`. Focus goes back to `opener`.
 */
export async function openDisplayNameDialog(
  dialog: MatDialog,
  opener?: HTMLElement,
): Promise<boolean> {
  const ref = dialog.open<DisplayNameDialog, void, boolean>(DisplayNameDialog, {
    autoFocus: 'first-tabbable',
    restoreFocus: opener ?? true,
  });
  return (await firstValueFrom(ref.afterClosed())) === true;
}
