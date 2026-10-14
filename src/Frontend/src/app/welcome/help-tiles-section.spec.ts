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

  const tiles = (): HTMLAnchorElement[] =>
    Array.from(host.querySelectorAll<HTMLAnchorElement>('a.help-tile'));

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
    expect(tiles().map((tile) => tile.getAttribute('href'))).toEqual(
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

  it('names every tile by its title alone and describes it with its summary', () => {
    tiles().forEach((tile, i) => {
      expect(accessibleName(tile)).toBe(HELP_TOPICS[i].title);
      const describedBy = tile.getAttribute('aria-describedby') ?? '';
      expect(document.getElementById(describedBy)?.textContent?.trim()).toBe(
        HELP_TOPICS[i].summary,
      );
    });
  });

  it('makes each tile a single focusable link with nothing focusable inside it', () => {
    for (const tile of tiles()) {
      expect(tile.tabIndex).toBe(0);
      expect(tile.querySelector('a, button, [tabindex]')).toBeNull();
    }
  });

  it('navigates to the resource commands topic without a session', async () => {
    const tile = tiles().find(
      (candidate) => accessibleName(candidate) === 'Custom resource commands',
    );
    expect(tile).toBeDefined();
    tile?.click();
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
