import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterOutlet } from '@angular/router';
import { ThemePreviewDialog } from './theme-preview-dialog';

@Component({
  imports: [
    RouterOutlet,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  selector: 'battle-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('battleship');

  // TEMPORARY theme preview state. Removed before the pull request.
  private readonly dialog = inject(MatDialog);
  protected readonly validName = new FormControl('Admiral Nelson', { nonNullable: true, validators: Validators.required });
  protected readonly invalidName = new FormControl('', { nonNullable: true, validators: Validators.required });

  constructor() {
    this.invalidName.markAsTouched();
  }

  protected openDialog(): void {
    this.dialog.open(ThemePreviewDialog);
  }
}
