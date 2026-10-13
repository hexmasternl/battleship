import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Placeholder for `/games/:gameCode` until the game page exists: names the game code so the
 * creator can share it. The route parameter is bound to the `gameCode` input.
 */
@Component({
  selector: 'battle-game-placeholder',
  imports: [RouterLink],
  template: `
    <main class="game-placeholder">
      <section
        class="bs-panel bs-panel--info game-placeholder__panel"
        aria-labelledby="game-placeholder-title"
      >
        <p class="game-placeholder__eyebrow">Game code</p>
        <h1 id="game-placeholder-title" class="game-placeholder__title">{{ gameCode() }}</h1>
        <p>
          Your game is ready. Share this code with a friend so they can join you. The battle station
          itself is still being fitted out.
        </p>
        <a class="bs-btn bs-btn--ghost game-placeholder__back" routerLink="/">Back to the bridge</a>
      </section>
    </main>
  `,
  styleUrl: './game-placeholder.scss',
})
export class GamePlaceholder {
  readonly gameCode = input.required<string>();
}
