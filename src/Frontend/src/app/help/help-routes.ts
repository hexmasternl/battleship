import { Routes } from '@angular/router';
import { HelpOverview } from './help-overview';
import { HelpTopicPage } from './help-topic-page';
import { HELP_TOPICS } from './help-topics';

/**
 * The help section, generated from the topic registry: the overview at `/help`, one route per
 * topic at `/help/<slug>` with the slug in route data, and every other path back to the overview.
 * Public: no guard, no session. Mounted lazily, so this file and everything it imports form the
 * help chunk.
 */
export const helpRoutes: Routes = [
  { path: '', pathMatch: 'full', title: 'Help | Battleship', component: HelpOverview },
  ...HELP_TOPICS.map((topic) => ({
    path: topic.slug,
    title: `${topic.title} | Help | Battleship`,
    data: { slug: topic.slug },
    component: HelpTopicPage,
  })),
  { path: '**', redirectTo: '' },
];
