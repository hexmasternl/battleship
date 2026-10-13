import { TestBed } from '@angular/core/testing';
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

  it('lazy loads the coming-soon placeholder for both game routes with their feature', () => {
    const create = route('games/new');
    expect(typeof create.loadComponent).toBe('function');
    expect(create.data).toEqual({ feature: 'create' });

    const join = route('games/join');
    expect(typeof join.loadComponent).toBe('function');
    expect(join.data).toEqual({ feature: 'join' });
  });

  it('redirects unknown paths to the landing page', () => {
    const wildcard = routes.at(-1);
    expect(wildcard?.path).toBe('**');
    expect(wildcard?.redirectTo).toBe('');
  });

  it('declares exactly three feature routes plus the wildcard', () => {
    expect(routes.map((candidate) => candidate.path)).toEqual([
      '',
      'games/new',
      'games/join',
      '**',
    ]);
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

  it('renders the create placeholder at /games/new', async () => {
    const harness = await RouterTestingHarness.create('/games/new');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Create a new game');
  });

  it('renders the join placeholder at /games/join', async () => {
    const harness = await RouterTestingHarness.create('/games/join');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Join an existing game');
  });

  it('sends unknown paths back to the landing page', async () => {
    const harness = await RouterTestingHarness.create('/no/such/page');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Welcome aboard, Captain');
  });
});
