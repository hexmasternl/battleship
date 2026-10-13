import { Component, inject, output } from '@angular/core';
import { FormField } from '@angular/forms/signals';
import { PlayerNameStore } from './player-name.store';

let nextId = 0;

/**
 * Asks the visitor for a player name and creates the player with it. Knows nothing about where it
 * is hosted: it emits `playerCreated` once the Players service has issued the tokens.
 */
@Component({
  selector: 'battle-player-name',
  imports: [FormField],
  providers: [PlayerNameStore],
  templateUrl: './player-name.html',
  styleUrl: './player-name.scss',
})
export class PlayerName {
  protected readonly store = inject(PlayerNameStore);

  /** Emits once the player has been created and the tokens are stored. */
  readonly playerCreated = output<void>();

  protected readonly inputId = `battle-player-name-${nextId++}`;
  protected readonly errorId = `${this.inputId}-error`;

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (await this.store.submit()) this.playerCreated.emit();
  }
}
