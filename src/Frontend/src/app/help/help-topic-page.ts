import { Component, computed, input } from '@angular/core';
import { findHelpTopic } from './help-topics';

/** One help topic at `/help/<slug>`; the slug arrives from route data. */
@Component({
  selector: 'battle-help-topic-page',
  template: `
    <main>
      <h1>{{ topic()?.title }}</h1>
    </main>
  `,
})
export class HelpTopicPage {
  /** The topic slug, bound from the route data by `withComponentInputBinding()`. */
  readonly slug = input.required<string>();

  protected readonly topic = computed(() => findHelpTopic(this.slug()));
}
