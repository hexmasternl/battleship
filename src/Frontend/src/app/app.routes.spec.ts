import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Route, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { helpRoutes } from './help/help-routes';
import { HELP_TOPICS } from './help/help-topics';
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

  it('lazy loads the help section without a guard', () => {
    const help = route('help');
    expect(typeof help.loadChildren).toBe('function');
    expect(help.canActivate).toBeUndefined();
    expect(help.canMatch).toBeUndefined();
    expect(help.canActivateChild).toBeUndefined();
  });

  it('declares exactly three feature routes plus the wildcard', () => {
    expect(routes.map((candidate) => candidate.path)).toEqual([
      '',
      'games/:gameCode',
      'help',
      '**',
    ]);
  });
});

describe('help routes', () => {
  it('declares the overview, one route per topic and a wildcard, none of them guarded', () => {
    expect(helpRoutes.map((candidate) => candidate.path)).toEqual([
      '',
      ...HELP_TOPICS.map((topic) => topic.slug),
      '**',
    ]);
    for (const candidate of helpRoutes) {
      expect(candidate.canActivate).toBeUndefined();
      expect(candidate.canMatch).toBeUndefined();
    }
  });

  it('carries the slug in the route data and names the topic in the title', () => {
    const dapr = helpRoutes.find((candidate) => candidate.path === 'dapr');
    expect(dapr?.data).toEqual({ slug: 'dapr' });
    expect(dapr?.title).toBe('Dapr | Help | Battleship');
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

  it('renders the help overview at /help without a session', async () => {
    const ensureUser = vi.spyOn(TestBed.inject(UserSession), 'ensureUser');
    const harness = await RouterTestingHarness.create('/help');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.tagName.toLowerCase()).toBe('battle-help-overview');
    expect(TestBed.inject(Title).getTitle()).toBe('Help | Battleship');
    expect(ensureUser).not.toHaveBeenCalled();
    TestBed.inject(HttpTestingController).expectNone((req) => req.url.startsWith('/api/'));
  });

  it('renders the topic page with its title at /help/dapr without a session', async () => {
    const ensureUser = vi.spyOn(TestBed.inject(UserSession), 'ensureUser');
    const harness = await RouterTestingHarness.create('/help/dapr');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.tagName.toLowerCase()).toBe('battle-help-topic-page');
    expect(TestBed.inject(Title).getTitle()).toBe('Dapr | Help | Battleship');
    expect(ensureUser).not.toHaveBeenCalled();
    TestBed.inject(HttpTestingController).expectNone((req) => req.url.startsWith('/api/'));
  });

  it('redirects an unknown help topic to the help overview', async () => {
    const harness = await RouterTestingHarness.create('/help/not-a-topic');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.tagName.toLowerCase()).toBe('battle-help-overview');
    expect(TestBed.inject(Router).url).toBe('/help');
  });

  it('sends unknown paths back to the landing page', async () => {
    const harness = await RouterTestingHarness.create('/no/such/page');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Welcome aboard, Captain');
  });
});
