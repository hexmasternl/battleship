import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MemoryStorage } from '../../testing/memory-storage';
import { PlayerSession } from '../core/session/player-session';
import {
  PERSISTENT_STORAGE,
  REFRESH_TOKEN_EXPIRES_AT_KEY,
  REFRESH_TOKEN_KEY,
  SESSION_STORAGE,
} from '../core/session/token-storage';
import { GAMES_URL } from '../games/games-api';
import { CONSOLE_MESSAGE_POOL, REDUCED_MOTION } from './console-scheduler';
import { HeroSection } from './hero-section';

@Component({ selector: 'battle-route-stub', template: '' })
class RouteStub {}

const CREATE_URL = '/api/users/tokens';
const REFRESH_URL = '/api/users/tokens/refresh';
const PAIR = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2999-01-01T00:00:00Z',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2999-01-01T00:00:00Z',
};
const CREATED = { gameId: 'b0c5', gameCode: 'K7Q2M9XA' };

describe('HeroSection', () => {
  let fixture: ComponentFixture<HeroSection>;
  let host: HTMLElement;
  let local: MemoryStorage;
  let backend: HttpTestingController;
  let router: Router;
  let dialog: MatDialog;

  const settle = async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await fixture.whenStable();
  };
  const dialogElement = () => document.querySelector('mat-dialog-container') as HTMLElement | null;
  const dialogTitle = () => dialogElement()?.querySelector('h2')?.textContent?.trim();
  const createButton = () => host.querySelector('.hero__actions button') as HTMLButtonElement;
  const joinLink = () => host.querySelector('.hero__actions a') as HTMLAnchorElement;

  async function becomeKnown(): Promise<void> {
    const created = TestBed.inject(PlayerSession).createPlayer('Admiral Nelson');
    backend.expectOne(CREATE_URL).flush(PAIR, { status: 201, statusText: 'Created' });
    await created;
  }

  async function waitForDialog(): Promise<void> {
    await firstValueFrom(dialog.openDialogs.at(-1)!.afterOpened());
    await settle();
  }

  /** Clicks a button in the open dialog's actions by its visible label. */
  function clickDialogAction(label: string): void {
    const button = Array.from(
      dialogElement()?.querySelectorAll<HTMLButtonElement>('mat-dialog-actions button') ?? [],
    ).find((b) => b.textContent?.trim() === label);
    if (button === undefined) throw new Error(`No dialog action "${label}"`);
    button.click();
  }

  beforeEach(async () => {
    local = new MemoryStorage();
    await TestBed.configureTestingModule({
      imports: [HeroSection],
      providers: [
        provideRouter([
          { path: 'games/join', component: RouteStub },
          { path: 'games/:gameCode', component: RouteStub },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PERSISTENT_STORAGE, useValue: () => local },
        { provide: SESSION_STORAGE, useValue: () => new MemoryStorage() },
        { provide: REDUCED_MOTION, useValue: true },
        { provide: CONSOLE_MESSAGE_POOL, useValue: ['Radar sweep clear.'] },
      ],
    }).compileComponents();
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    dialog = TestBed.inject(MatDialog);
    fixture = TestBed.createComponent(HeroSection);
    host = fixture.nativeElement as HTMLElement;
    document.body.appendChild(host);
    await fixture.whenStable();
  });

  afterEach(() => {
    dialog.closeAll();
    host.remove();
    backend.verify();
  });

  it('shows one level-one welcome heading and an intro paragraph', () => {
    const headings = host.querySelectorAll('h1');
    expect(headings.length).toBe(1);
    expect(headings[0].textContent?.trim()).toBe('Welcome aboard, Captain');
    expect(host.querySelector('.hero__intro')?.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('offers a create button and a join link, styled by role', () => {
    const actions = host.querySelector('.hero__actions') as HTMLElement;
    expect(actions.children.length).toBe(2);

    const create = createButton();
    expect(create.textContent?.trim()).toBe('Create a new game');
    expect(create.getAttribute('type')).toBe('button');
    expect(create.hasAttribute('href')).toBe(false);
    expect(create.classList.contains('bs-btn')).toBe(true);
    expect(create.classList.contains('bs-btn--primary')).toBe(true);

    const join = joinLink();
    expect(join.textContent?.trim()).toBe('Join an existing game');
    expect(join.getAttribute('href')).toBe('/games/join');
    expect(join.classList.contains('bs-btn')).toBe(true);
    expect(join.classList.contains('bs-btn--secondary')).toBe(true);
  });

  it('places the welcome panel before the bridge console', () => {
    const jumbotron = host.querySelector('.hero__jumbotron') as HTMLElement;
    const children = Array.from(jumbotron.children);
    expect(children[0].classList.contains('hero__welcome')).toBe(true);
    expect(children[1].tagName.toLowerCase()).toBe('battle-bridge-console');
  });

  it('keeps the radar motif decorative', () => {
    const radar = host.querySelector('.hero__radar');
    expect(radar?.getAttribute('aria-hidden')).toBe('true');
    expect(host.querySelector('img')).toBeNull();
  });

  describe('"Create a new game"', () => {
    async function activate(): Promise<void> {
      createButton().focus();
      createButton().click();
      await settle();
    }

    it('asks a known player to confirm, with focus in the dialog and no navigation', async () => {
      await becomeKnown();

      await activate();
      await waitForDialog();

      expect(router.url).toBe('/');
      expect(dialogTitle()).toBe('Create a new game');
      expect(dialogElement()?.contains(document.activeElement)).toBe(true);
      backend.expectNone(GAMES_URL);
    });

    it('creates the game on confirm and navigates to it', async () => {
      await becomeKnown();
      await activate();
      await waitForDialog();

      clickDialogAction('Create game');
      await settle();
      backend.expectOne(GAMES_URL).flush(CREATED, { status: 201, statusText: 'Created' });
      await firstValueFrom(dialog.afterAllClosed);
      await settle();

      expect(dialog.openDialogs.length).toBe(0);
      expect(router.url).toBe('/games/K7Q2M9XA');
    });

    it('asks an unknown visitor for a name, then for confirmation', async () => {
      await activate();
      await waitForDialog();

      expect(router.url).toBe('/');
      expect(dialogTitle()).toBe('Choose your player name');
      expect(dialogElement()?.contains(document.activeElement)).toBe(true);

      const input = dialogElement()?.querySelector('input') as HTMLInputElement;
      input.value = 'Admiral Nelson';
      input.dispatchEvent(new Event('input'));
      (dialogElement()?.querySelector('button[type=submit]') as HTMLButtonElement).click();
      await settle();
      backend.expectOne(CREATE_URL).flush(PAIR, { status: 201, statusText: 'Created' });
      await firstValueFrom(dialog.afterAllClosed);
      await settle();
      await waitForDialog();

      expect(dialog.openDialogs.length).toBe(1);
      expect(dialogTitle()).toBe('Create a new game');
      expect(router.url).toBe('/');
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
    });

    it('stays here, sends nothing and returns focus when the confirmation is cancelled', async () => {
      await becomeKnown();
      await activate();
      await waitForDialog();

      clickDialogAction('Cancel');
      await firstValueFrom(dialog.afterAllClosed);
      await settle();

      backend.expectNone(GAMES_URL);
      expect(router.url).toBe('/');
      expect(document.activeElement).toBe(createButton());
    });

    it('stays here and returns focus when the name dialog is cancelled', async () => {
      await activate();
      await waitForDialog();

      clickDialogAction('Cancel');
      await firstValueFrom(dialog.afterAllClosed);
      await settle();

      expect(dialog.openDialogs.length).toBe(0);
      expect(router.url).toBe('/');
      expect(local.keys()).toEqual([]);
      expect(document.activeElement).toBe(createButton());
    });

    it('keeps the dialog open with an error and does not navigate when creation fails', async () => {
      await becomeKnown();
      await activate();
      await waitForDialog();

      clickDialogAction('Create game');
      await settle();
      backend.expectOne(GAMES_URL).flush(null, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(dialog.openDialogs.length).toBe(1);
      expect(dialogElement()?.querySelector('[role=alert]')).not.toBeNull();
      expect(router.url).toBe('/');
    });

    it('sends one request when confirm is activated twice while pending', async () => {
      await becomeKnown();
      await activate();
      await waitForDialog();

      clickDialogAction('Create game');
      await settle();
      clickDialogAction('Create game');
      await settle();

      backend.expectOne(GAMES_URL).flush(CREATED, { status: 201, statusText: 'Created' });
      await firstValueFrom(dialog.afterAllClosed);
      await settle();
      expect(router.url).toBe('/games/K7Q2M9XA');
    });

    it('opens one dialog when activated twice', async () => {
      await becomeKnown();
      await activate();
      await activate();
      await waitForDialog();

      expect(dialog.openDialogs.length).toBe(1);
    });

    it('waits for a pending refresh and asks for confirmation without a name dialog', async () => {
      local.setItem(REFRESH_TOKEN_KEY, 'refresh-0');
      local.setItem(REFRESH_TOKEN_EXPIRES_AT_KEY, '2999-01-01T00:00:00Z');
      void TestBed.inject(PlayerSession).restore();

      await activate();
      expect(dialogElement()).toBeNull();

      backend.expectOne(REFRESH_URL).flush(PAIR);
      await settle();
      await waitForDialog();

      expect(dialog.openDialogs.length).toBe(1);
      expect(dialogTitle()).toBe('Create a new game');
      expect(router.url).toBe('/');
    });
  });

  describe('"Join an existing game"', () => {
    async function activate(): Promise<void> {
      joinLink().focus();
      joinLink().click();
      await settle();
    }

    it('navigates a known player at once without a dialog', async () => {
      await becomeKnown();

      await activate();

      expect(dialogElement()).toBeNull();
      expect(router.url).toBe('/games/join');
    });

    it('asks an unknown visitor for a name and navigates once the player is created', async () => {
      await activate();

      expect(router.url).toBe('/');
      await waitForDialog();
      expect(dialogElement()?.contains(document.activeElement)).toBe(true);

      const input = dialogElement()?.querySelector('input') as HTMLInputElement;
      input.value = 'Admiral Nelson';
      input.dispatchEvent(new Event('input'));
      (dialogElement()?.querySelector('button[type=submit]') as HTMLButtonElement).click();
      await settle();
      const req = backend.expectOne(CREATE_URL);
      expect(req.request.body).toEqual({ displayName: 'Admiral Nelson' });
      req.flush(PAIR, { status: 201, statusText: 'Created' });
      await firstValueFrom(dialog.afterAllClosed);
      await settle();

      expect(dialog.openDialogs.length).toBe(0);
      expect(router.url).toBe('/games/join');
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
    });

    it('stays on the landing page and returns focus when the dialog is cancelled', async () => {
      await activate();
      await waitForDialog();

      clickDialogAction('Cancel');
      await firstValueFrom(dialog.afterAllClosed);
      await settle();

      expect(router.url).toBe('/');
      expect(local.keys()).toEqual([]);
      expect(document.activeElement).toBe(joinLink());
    });

    it('waits for a pending refresh and navigates without a dialog when it succeeds', async () => {
      local.setItem(REFRESH_TOKEN_KEY, 'refresh-0');
      local.setItem(REFRESH_TOKEN_EXPIRES_AT_KEY, '2999-01-01T00:00:00Z');
      void TestBed.inject(PlayerSession).restore();

      await activate();
      expect(router.url).toBe('/');
      expect(dialogElement()).toBeNull();

      backend.expectOne(REFRESH_URL).flush(PAIR);
      await settle();
      await settle();

      expect(dialogElement()).toBeNull();
      expect(router.url).toBe('/games/join');
    });

    it('leaves modified clicks to the browser', async () => {
      const event = new MouseEvent('click', { bubbles: true, cancelable: true, ctrlKey: true });
      joinLink().dispatchEvent(event);
      await settle();

      expect(event.defaultPrevented).toBe(false);
      expect(dialogElement()).toBeNull();
      expect(router.url).toBe('/');
    });
  });
});
