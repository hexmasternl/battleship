import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Route, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { CONSOLE_MESSAGE_POOL, REDUCED_MOTION } from './welcome/console-scheduler';

function route(path: string): Route {
  const found = routes.find((candidate) => candidate.path === path);
  if (found === undefined) throw new Error(`No route for path "${path}"`);
  return found;
}

describe('routes', () => {
  it('lazy loads the landing page at the root path', () => {
    const root = route('');
    expect(root.pathMatch).toBe('full');
    expect(typeof root.loadComponent).toBe('function');
  });

  it('lazy loads the game placeholder for a game code', () => {
    const game = route('games/:gameCode');
    expect(typeof game.loadComponent).toBe('function');
  });

  it.each(['games/new', 'games/join'])('no longer declares %s', (path) => {
    expect(routes.some((candidate) => candidate.path === path)).toBe(false);
  });

  it('redirects unknown paths to the landing page', () => {
    const wildcard = routes.at(-1);
    expect(wildcard?.path).toBe('**');
    expect(wildcard?.redirectTo).toBe('');
  });

  it('declares exactly two feature routes plus the wildcard', () => {
    expect(routes.map((candidate) => candidate.path)).toEqual(['', 'games/:gameCode', '**']);
  });
});

describe('routes (navigation)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        { provide: REDUCED_MOTION, useValue: true },
        { provide: CONSOLE_MESSAGE_POOL, useValue: ['Radar sweep clear.'] },
      ],
    });
  });

  it('renders the landing page at /', async () => {
    const harness = await RouterTestingHarness.create('/');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Welcome aboard, Captain');
    expect(element.querySelector('battle-bridge-console')).not.toBeNull();
    expect(element.querySelector('battle-info-section')).not.toBeNull();
  });

  it('renders the game placeholder naming the code at /games/K7Q2M9XA', async () => {
    const harness = await RouterTestingHarness.create('/games/K7Q2M9XA');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.tagName.toLowerCase()).toBe('battle-game-placeholder');
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('K7Q2M9XA');
    expect(TestBed.inject(Title).getTitle()).toBe('Game K7Q2M9XA | Battleship');
  });

  it.each(['/games/new', '/games/join', '/games/k7q2m9xa', '/games/K7Q2M9X'])(
    'sends %s, which is not a game code, back to the landing page',
    async (url) => {
      const harness = await RouterTestingHarness.create(url);
      const element = harness.routeNativeElement as HTMLElement;
      expect(element.querySelector('h1')?.textContent?.trim()).toBe('Welcome aboard, Captain');
    },
  );

  it('sends unknown paths back to the landing page', async () => {
    const harness = await RouterTestingHarness.create('/no/such/page');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Welcome aboard, Captain');
  });
});
