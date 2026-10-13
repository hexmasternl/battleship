import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** The game flows that do not exist yet. The value comes from the route's `data.feature`. */
export type ComingSoonFeature = 'create' | 'join';

const FEATURE_TITLES: Record<ComingSoonFeature, string> = {
  create: 'Create a new game',
  join: 'Join an existing game',
};

/**
 * Placeholder for the create-game and join-game routes until those flows are built.
 * One component serves both: the route's `data.feature` is bound to the `feature` input.
 */
@Component({
  selector: 'battle-coming-soon',
  imports: [RouterLink],
  template: `
    <main class="coming-soon">
      <section
        class="bs-panel bs-panel--info coming-soon__panel"
        aria-labelledby="coming-soon-title"
      >
        <p class="coming-soon__eyebrow">Coming soon</p>
        <h1 id="coming-soon-title" class="coming-soon__title">{{ title() }}</h1>
        <p>
          This station is still being fitted out. The crew is wiring the consoles and will report
          when it is ready for action.
        </p>
        <a class="bs-btn bs-btn--ghost coming-soon__back" routerLink="/">Back to the bridge</a>
      </section>
    </main>
  `,
  styleUrl: './coming-soon.scss',
})
export class ComingSoon {
  readonly feature = input.required<ComingSoonFeature>();

  protected readonly title = computed(() => FEATURE_TITLES[this.feature()]);
}
