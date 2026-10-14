import { Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { UserSession } from '../core/session/user-session';
import { openCreateGameDialog } from '../games/create-game-dialog/create-game-dialog';
import { openJoinGameDialog } from '../games/join-game-dialog/join-game-dialog';
import { openDisplayNameDialog } from '../shared/display-name/display-name-dialog';
import { BridgeConsole } from './bridge-console';
import { SubmarineInterior } from './submarine-interior';

/** What a game dialog closes with: the game to navigate to, or `null` when cancelled. */
type GameDialogOutcome = Promise<{ readonly gameCode: string } | null>;

/**
 * The full-viewport hero of the landing page: the animated submarine control room as a
 * full-bleed backdrop, and the two-panel jumbotron with the welcome copy, the calls to action
 * and the bridge console.
 *
 * Both calls to action make sure the visitor is a known user (asking an unknown one for a name
 * first). "Create a new game" then asks for confirmation in `CreateGameDialog`, "Join an existing
 * game" asks for a code in `JoinGameDialog`, and either navigates to the game. A single busy flag
 * is all the state there is, so no store.
 */
@Component({
  selector: 'battle-hero-section',
  imports: [BridgeConsole, SubmarineInterior],
  host: { class: 'hero' },
  templateUrl: './hero-section.html',
  styleUrl: './hero-section.scss',
})
export class HeroSection {
  readonly #session = inject(UserSession);
  readonly #dialog = inject(MatDialog);
  readonly #router = inject(Router);

  /** Whether a call-to-action flow is running; a second activation then opens no second dialog. */
  readonly #busy = signal(false);

  /** Asks to confirm a new game and navigates to it. */
  protected createGame(opener: HTMLElement): Promise<void> {
    return this.#playThen(opener, () => openCreateGameDialog(this.#dialog, opener));
  }

  /** Asks for a game code, joins that game and navigates to it. */
  protected joinGame(opener: HTMLElement): Promise<void> {
    return this.#playThen(opener, () => openJoinGameDialog(this.#dialog, opener));
  }

  /**
   * Waits for a pending restore, asks an unknown visitor for a player name, then opens the game
   * dialog and navigates to the game it closes with. Dismissing either dialog leaves the visitor
   * here, with focus back on `opener`.
   */
  async #playThen(opener: HTMLElement, openGameDialog: () => GameDialogOutcome): Promise<void> {
    if (this.#busy()) return;
    this.#busy.set(true);
    try {
      const known =
        (await this.#session.ensureUser()) || (await openDisplayNameDialog(this.#dialog, opener));
      if (!known) return;
      const game = await openGameDialog();
      if (game !== null) await this.#router.navigate(['/games', game.gameCode]);
    } finally {
      this.#busy.set(false);
    }
  }
}
