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

  beforeEach(async () => {
    local = new MemoryStorage();
    await TestBed.configureTestingModule({
      imports: [HeroSection],
      providers: [
        provideRouter([
          { path: 'games/new', component: RouteStub },
          { path: 'games/join', component: RouteStub },
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

  it('offers exactly two calls to action styled by role', () => {
    const links = host.querySelectorAll<HTMLAnchorElement>('.hero__actions a');
    expect(links.length).toBe(2);

    const [create, join] = Array.from(links);
    expect(create.textContent?.trim()).toBe('Create a new game');
    expect(create.getAttribute('href')).toBe('/games/new');
    expect(create.classList.contains('bs-btn')).toBe(true);
    expect(create.classList.contains('bs-btn--primary')).toBe(true);

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

  describe.each([
    ['Create a new game', '/games/new'],
    ['Join an existing game', '/games/join'],
  ])('"%s"', (label, route) => {
    const link = () =>
      Array.from(host.querySelectorAll<HTMLAnchorElement>('.hero__actions a')).find(
        (a) => a.textContent?.trim() === label,
      ) as HTMLAnchorElement;

    async function activate(): Promise<void> {
      link().focus();
      link().click();
      await settle();
    }

    it('navigates a known player at once without a dialog', async () => {
      const created = TestBed.inject(PlayerSession).createPlayer('Admiral Nelson');
      backend.expectOne(CREATE_URL).flush(PAIR, { status: 201, statusText: 'Created' });
      await created;

      await activate();

      expect(dialogElement()).toBeNull();
      expect(router.url).toBe(route);
    });

    it('asks an unknown visitor for a name and navigates once the player is created', async () => {
      await activate();

      expect(router.url).toBe('/');
      expect(dialogElement()).not.toBeNull();
      await firstValueFrom(dialog.openDialogs[0].afterOpened());
      await settle();
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
      expect(router.url).toBe(route);
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
    });

    it('stays on the landing page and returns focus when the dialog is cancelled', async () => {
      await activate();
      await firstValueFrom(dialog.openDialogs[0].afterOpened());

      (dialogElement()?.querySelector('mat-dialog-actions button') as HTMLButtonElement).click();
      await firstValueFrom(dialog.afterAllClosed);
      await settle();

      expect(router.url).toBe('/');
      expect(local.keys()).toEqual([]);
      expect(document.activeElement).toBe(link());
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
      expect(router.url).toBe(route);
    });

    it('leaves modified clicks to the browser', async () => {
      const event = new MouseEvent('click', { bubbles: true, cancelable: true, ctrlKey: true });
      link().dispatchEvent(event);
      await settle();

      expect(event.defaultPrevented).toBe(false);
      expect(dialogElement()).toBeNull();
      expect(router.url).toBe('/');
    });
  });
});
