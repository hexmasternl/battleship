import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { HELP_TOPICS, HelpTopic } from '../help/help-topics';

/** A help topic as a landing tile: the registry entry plus its two-digit position. */
export interface HelpTile extends HelpTopic {
  readonly index: number;
  readonly number: string;
}

/** One tile per help topic, in reading order. */
export const HELP_TILES: readonly HelpTile[] = HELP_TOPICS.map((topic, index) => ({
  ...topic,
  index,
  number: String(index + 1).padStart(2, '0'),
}));

/**
 * The help tiles below the information section: one tile per help topic, deep-linking into the
 * public help section, plus a link to the help overview. It imports only the topic registry, which
 * holds metadata, so the markdown renderer stays in the lazy help chunk. Static copy, no state, so
 * no store.
 */
@Component({
  selector: 'battle-help-tiles-section',
  host: { class: 'help-tiles' },
  imports: [RouterLink, MatIconModule],
  templateUrl: './help-tiles-section.html',
  styleUrl: './help-tiles-section.scss',
})
export class HelpTilesSection {
  protected readonly tiles = HELP_TILES;
}
