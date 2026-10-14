import { Component, inject } from '@angular/core';
import { UserSession } from '../core/session/user-session';
import { HeroSection } from './hero-section';
import { HelpTilesSection } from './help-tiles-section';
import { InfoSection } from './info-section';

/**
 * The landing page at `/`: the full-viewport hero, the information section and the help tiles.
 * Every visit restores a returning user in the background; the page renders without waiting for it.
 */
@Component({
  selector: 'battle-welcome-page',
  imports: [HeroSection, InfoSection, HelpTilesSection],
  template: `
    <main>
      <battle-hero-section />
      <battle-info-section />
      <battle-help-tiles-section />
    </main>
  `,
  styles: `
    :host {
      display: block;
      background: var(--bs-color-bg);
      color: var(--bs-color-text);
    }
  `,
})
export class WelcomePage {
  constructor() {
    void inject(UserSession).restore();
  }
}
