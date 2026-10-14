import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import axeCore from 'axe-core';
import { axe } from 'vitest-axe';
import { routes } from '../app.routes';
import { HELP_TOPICS } from '../help/help-topics';
import { HELP_TILES, HelpTilesSection } from './help-tiles-section';

/** The accessible name of an element as axe computes it (accname 1.2). */
function accessibleName(element: Element): string {
  axeCore.setup(element.ownerDocument);
  try {
    return axeCore.commons.text.accessibleText(element).trim();
  } finally {
    axeCore.teardown();
  }
}

describe('HelpTilesSection', () => {
  let fixture: ComponentFixture<HelpTilesSection>;
  let host: HTMLElement;
  let backend: HttpTestingController;

  const tiles = (): HTMLElement[] => Array.from(host.querySelectorAll<HTMLElement>('.help-tile'));
  const links = (): HTMLAnchorElement[] =>
    Array.from(host.querySelectorAll<HTMLAnchorElement>('a.help-tile__link'));

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HelpTilesSection],
      // The app's real routes, with no session providers: the help section must not need one.
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HelpTilesSection);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  it('shows a heading and a link to the help overview', () => {
    expect(host.querySelector('h2')?.textContent?.trim()).toBe('How this demo is built');
    const all = host.querySelector<HTMLAnchorElement>('a.help-tiles__all');
    expect(all?.getAttribute('href')).toBe('/help');
    expect(all?.textContent).toContain('Browse all help');
  });

  it('shows one tile per help topic, in registry order, each linking to its topic', () => {
    expect(tiles().length).toBe(HELP_TOPICS.length);
    expect(links().map((link) => link.getAttribute('href'))).toEqual(
      HELP_TOPICS.map((topic) => `/help/${topic.slug}`),
    );
  });

  it('shows the icon, title and summary of every topic in its tile', () => {
    tiles().forEach((tile, i) => {
      const topic = HELP_TOPICS[i];
      expect(tile.querySelector('.help-tile__icon')?.textContent?.trim()).toBe(topic.icon);
      expect(tile.querySelector('.help-tile__title')?.textContent?.trim()).toBe(topic.title);
      expect(tile.querySelector('.help-tile__summary')?.textContent?.trim()).toBe(topic.summary);
    });
  });

  it('numbers the tiles with two digits', () => {
    expect(HELP_TILES.map((tile) => tile.number)).toEqual(['01', '02', '03', '04', '05', '06']);
  });

  it('names every tile link by its title alone and describes it with its summary', () => {
    links().forEach((link, i) => {
      expect(accessibleName(link)).toBe(HELP_TOPICS[i].title);
      // The visible text of the link is exactly its name (WCAG 2.5.3, label in name).
      expect(link.textContent?.trim()).toBe(HELP_TOPICS[i].title);
      const describedBy = link.getAttribute('aria-describedby') ?? '';
      expect(document.getElementById(describedBy)?.textContent?.trim()).toBe(
        HELP_TOPICS[i].summary,
      );
    });
  });

  it('gives each tile exactly one focusable element: its link', () => {
    for (const tile of tiles()) {
      const focusable = tile.querySelectorAll('a[href], button, [tabindex]');
      expect(focusable.length).toBe(1);
      expect((focusable[0] as HTMLElement).tabIndex).toBe(0);
    }
  });

  it('navigates to the resource commands topic without a session', async () => {
    const link = links().find(
      (candidate) => accessibleName(candidate) === 'Custom resource commands',
    );
    expect(link).toBeDefined();
    link?.click();
    await fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/help/resource-commands');
    // The topic page fetches only its document; nothing goes to the backend.
    const requests = backend.match(() => true).map((req) => req.request.url);
    expect(requests.every((url) => !url.includes('/api/'))).toBe(true);
  });

  it('has no accessibility violations', async () => {
    const results = await axe(host);
    const summary = results.violations.map(
      (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
    );
    expect(summary).toEqual([]);
  }, 20000);
});
