import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { HelpLayout } from './help-layout';
import { HELP_TOPICS } from './help-topics';

/** The help overview at `/help`: one link per topic, in reading order, from the registry. */
@Component({
  selector: 'battle-help-overview',
  imports: [HelpLayout, RouterLink, MatIconModule],
  templateUrl: './help-overview.html',
  styleUrl: './help-overview.scss',
})
export class HelpOverview {
  protected readonly topics = HELP_TOPICS;
}
