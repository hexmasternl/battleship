import { Component, computed, input } from '@angular/core';
import { HelpDocument } from './help-document';
import { HelpLayout } from './help-layout';
import { findHelpTopic } from './help-topics';

/**
 * One help topic at `/help/<slug>`: the help layout around the topic's markdown document. The
 * document's own `# Title` is the page's only level-one heading.
 */
@Component({
  selector: 'battle-help-topic-page',
  imports: [HelpLayout, HelpDocument],
  template: `
    <battle-help-layout [slug]="slug()">
      @if (topic(); as current) {
        <battle-help-document [file]="current.file" />
      }
    </battle-help-layout>
  `,
})
export class HelpTopicPage {
  /** The topic slug, bound from the route data by `withComponentInputBinding()`. */
  readonly slug = input.required<string>();

  protected readonly topic = computed(() => findHelpTopic(this.slug()));
}
