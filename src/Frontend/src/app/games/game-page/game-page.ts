import { Component, input } from '@angular/core';

/** The page of one game at `/games/:gameCode`. The route parameter binds to `gameCode`. */
@Component({
  selector: 'battle-game-page',
  template: `
    <main class="game-page">
      <h1 class="game-page__title">Game {{ gameCode() }}</h1>
    </main>
  `,
})
export class GamePage {
  readonly gameCode = input.required<string>();
}
