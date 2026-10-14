import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { findHelpTopic, helpNeighbours } from './help-topics';

/**
 * The frame of every help page: the main landmark, a breadcrumb back to the landing page and the
 * help overview, the page content, and, on a topic page, links to the previous and next topic.
 * Without a slug it frames the overview.
 */
@Component({
  selector: 'battle-help-layout',
  imports: [RouterLink, MatIconModule],
  templateUrl: './help-layout.html',
  styleUrl: './help-layout.scss',
})
export class HelpLayout {
  /** The slug of the topic shown; `null` on the overview. */
  readonly slug = input<string | null>(null);

  protected readonly topic = computed(() => {
    const slug = this.slug();
    return slug === null ? undefined : findHelpTopic(slug);
  });

  protected readonly neighbours = computed(() => helpNeighbours(this.slug() ?? ''));
}
