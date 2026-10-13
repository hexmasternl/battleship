import { Component } from '@angular/core';
import { HeroSection } from './hero-section';
import { InfoSection } from './info-section';

/** The landing page at `/`: the full-viewport hero followed by the information section. */
@Component({
  selector: 'battle-welcome-page',
  imports: [HeroSection, InfoSection],
  template: `
    <main>
      <battle-hero-section />
      <battle-info-section />
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
export class WelcomePage {}
