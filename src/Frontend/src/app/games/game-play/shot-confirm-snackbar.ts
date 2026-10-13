import { Component, Signal, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_SNACK_BAR_DATA, MatSnackBarModule } from '@angular/material/snack-bar';

/** What the confirm snackbar shows and does; the signals keep it current while it is open. */
export interface ShotConfirmData {
  /** The target's coordinate, such as `E7`. Follows the target when it moves. */
  readonly cell: Signal<string>;
  /** A shot is in flight: Shoot is disabled until it is answered. */
  readonly firing: Signal<boolean>;
  shoot(): void;
  cancel(): void;
}

/**
 * The snackbar that confirms a shot: names the target cell, with Cancel and Shoot. Opened by
 * `GamePlayPhase` while a target is selected; it stays until the target is cleared.
 */
@Component({
  selector: 'battle-shot-confirm-snackbar',
  imports: [MatButtonModule, MatSnackBarModule],
  template: `
    <span matSnackBarLabel>Fire at {{ data.cell() }}?</span>
    <span matSnackBarActions>
      <button matButton matSnackBarAction type="button" (click)="data.cancel()">Cancel</button>
      <button
        matButton
        matSnackBarAction
        type="button"
        class="shot-confirm__shoot"
        [disabled]="data.firing()"
        (click)="data.shoot()"
      >
        Shoot
      </button>
    </span>
  `,
})
export class ShotConfirmSnackbar {
  protected readonly data = inject<ShotConfirmData>(MAT_SNACK_BAR_DATA);
}
