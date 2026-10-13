import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Route, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { UserSession } from './core/session/user-session';
import { userGuard } from './core/session/user.guard';
import { EVENT_SOURCE_FACTORY, EVENTS_API_URL } from './events/events.tokens';
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

  it('lazy loads the game page behind the user guard for a game code', () => {
    const game = route('games/:gameCode');
    expect(typeof game.loadComponent).toBe('function');
    expect(game.canActivate).toEqual([userGuard]);
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
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: EVENTS_API_URL, useValue: 'http://events.test' },
        {
          provide: EVENT_SOURCE_FACTORY,
          useValue: () => ({
            readyState: 0,
            addEventListener: () => undefined,
            close: () => undefined,
          }),
        },
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

  it('renders the game page for a known user at /games/K7Q2M9XA', async () => {
    vi.spyOn(TestBed.inject(UserSession), 'ensureUser').mockResolvedValue(true);
    const harness = await RouterTestingHarness.create('/games/K7Q2M9XA');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.tagName.toLowerCase()).toBe('battle-game-page');
    expect(TestBed.inject(Title).getTitle()).toBe('Game K7Q2M9XA | Battleship');
  });

  it('sends a visitor who is not a known user to the landing page without requesting the game', async () => {
    const harness = await RouterTestingHarness.create('/games/K7Q2M9XA');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Welcome aboard, Captain');
    TestBed.inject(HttpTestingController).expectNone((req) => req.url.includes('/api/games'));
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
