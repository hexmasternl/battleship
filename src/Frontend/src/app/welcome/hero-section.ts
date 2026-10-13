import { Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Router, RouterLink } from '@angular/router';
import { PlayerSession } from '../core/session/player-session';
import { openCreateGameDialog } from '../games/create-game-dialog/create-game-dialog';
import { openPlayerNameDialog } from '../shared/player-name/player-name-dialog';
import { RequirePlayer } from '../shared/player-name/require-player';
import { BridgeConsole } from './bridge-console';

/**
 * The full-viewport hero of the landing page: a marine background built from design system
 * tokens, a decorative radar, and the two-panel jumbotron with the welcome copy, the calls to
 * action and the bridge console.
 *
 * "Create a new game" makes sure the visitor is a known player (asking an unknown one for a name
 * first), asks for confirmation in `CreateGameDialog` and navigates to the new game. "Join an
 * existing game" is a link gated by `RequirePlayer`. A single busy flag is all the state there is,
 * so no store.
 */
@Component({
  selector: 'battle-hero-section',
  imports: [RouterLink, RequirePlayer, BridgeConsole],
  host: { class: 'hero' },
  templateUrl: './hero-section.html',
  styleUrl: './hero-section.scss',
})
export class HeroSection {
  readonly #session = inject(PlayerSession);
  readonly #dialog = inject(MatDialog);
  readonly #router = inject(Router);

  /** Whether a create flow is running; a second activation then opens no second dialog. */
  readonly #creating = signal(false);

  /**
   * Waits for a pending restore, asks an unknown visitor for a player name, then asks to confirm
   * the new game and navigates to it. Dismissing either dialog leaves the visitor here, with focus
   * back on `opener`.
   */
  protected async createGame(opener: HTMLElement): Promise<void> {
    if (this.#creating()) return;
    this.#creating.set(true);
    try {
      const known =
        (await this.#session.ensurePlayer()) || (await openPlayerNameDialog(this.#dialog, opener));
      if (!known) return;
      const created = await openCreateGameDialog(this.#dialog, opener);
      if (created !== null) await this.#router.navigate(['/games', created.gameCode]);
    } finally {
      this.#creating.set(false);
    }
  }
}
