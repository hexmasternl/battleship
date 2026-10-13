// TEMPORARY: theme preview dialog used to verify the Material theme. Removed before the pull request.
import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';

@Component({
  selector: 'battle-theme-preview-dialog',
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Engage the fleet?</h2>
    <mat-dialog-content>
      <p>The dialog surface is a step of the design system surface scale. Body copy is Courier Prime.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>Stand down</button>
      <button matButton="filled" mat-dialog-close cdkFocusInitial>Fire</button>
    </mat-dialog-actions>
  `,
})
export class ThemePreviewDialog {}
