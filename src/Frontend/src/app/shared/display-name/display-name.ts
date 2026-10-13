import { Component, inject, output } from '@angular/core';
import { FormField } from '@angular/forms/signals';
import { DisplayNameStore } from './display-name.store';

let nextId = 0;

/**
 * Asks the visitor for a player name and creates the user with it. Knows nothing about where it
 * is hosted: it emits `userCreated` once the Users service has issued the tokens.
 */
@Component({
  selector: 'battle-display-name',
  imports: [FormField],
  providers: [DisplayNameStore],
  templateUrl: './display-name.html',
  styleUrl: './display-name.scss',
})
export class DisplayName {
  protected readonly store = inject(DisplayNameStore);

  /** Emits once the user has been created and the tokens are stored. */
  readonly userCreated = output<void>();

  protected readonly inputId = `battle-display-name-${nextId++}`;
  protected readonly errorId = `${this.inputId}-error`;

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (await this.store.submit()) this.userCreated.emit();
  }
}
