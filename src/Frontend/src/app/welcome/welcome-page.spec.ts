import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { MemoryStorage } from '../../testing/memory-storage';
import { PlayerSession } from '../core/session/player-session';
import {
  PERSISTENT_STORAGE,
  REFRESH_TOKEN_EXPIRES_AT_KEY,
  REFRESH_TOKEN_KEY,
  SESSION_STORAGE,
} from '../core/session/token-storage';
import { CONSOLE_MESSAGE_POOL, CONSOLE_RANDOM, REDUCED_MOTION } from './console-scheduler';
import { WelcomePage } from './welcome-page';

const REFRESH_URL = '/api/users/tokens/refresh';

describe('WelcomePage', () => {
  let fixture: ComponentFixture<WelcomePage>;
  let host: HTMLElement;
  let local: MemoryStorage;
  let backend: HttpTestingController;

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(WelcomePage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  beforeEach(async () => {
    local = new MemoryStorage();
    const session = new MemoryStorage();
    await TestBed.configureTestingModule({
      imports: [WelcomePage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PERSISTENT_STORAGE, useValue: () => local },
        { provide: SESSION_STORAGE, useValue: () => session },
        { provide: REDUCED_MOTION, useValue: true },
        // Draw 0 so the first console line is on screen at once and is part of the axe run.
        { provide: CONSOLE_RANDOM, useValue: () => 0 },
        { provide: CONSOLE_MESSAGE_POOL, useValue: ['Radar sweep clear.', 'Sonar ping sent.'] },
      ],
    }).compileComponents();
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  describe('without a stored refresh token', () => {
    beforeEach(render);

    it('composes the hero and the information section inside a main landmark', () => {
      const main = host.querySelector('main');
      expect(main).not.toBeNull();
      expect(main?.querySelector('battle-hero-section')).not.toBeNull();
      expect(main?.querySelector('battle-info-section')).not.toBeNull();
      expect(host.querySelectorAll('h1').length).toBe(1);
    });

    it('sends no refresh request and leaves the player unknown', () => {
      backend.expectNone(REFRESH_URL);
      expect(TestBed.inject(PlayerSession).status()).toBe('unknown');
    });

    it('has no accessibility violations', async () => {
      // Let the first console line land (0ms draw), then run axe on the attached fixture.
      // jsdom cannot resolve colours from custom properties, so contrast is covered by the
      // throwaway script recorded in README.md rather than by this run.
      await new Promise((resolve) => setTimeout(resolve, 0));
      await fixture.whenStable();
      expect(host.querySelector('.console__line')).not.toBeNull();

      const results = await axe(host);
      const summary = results.violations.map(
        (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
      );
      expect(summary).toEqual([]);
    }, 20000);
  });

  describe('with a stored refresh token', () => {
    beforeEach(async () => {
      local.setItem(REFRESH_TOKEN_KEY, 'refresh-0');
      local.setItem(REFRESH_TOKEN_EXPIRES_AT_KEY, '2999-01-01T00:00:00Z');
      await render();
    });

    it('sends the refresh request on creation and renders the hero without waiting for it', async () => {
      const req = backend.expectOne(REFRESH_URL);
      expect(req.request.body).toEqual({ refreshToken: 'refresh-0' });

      // The response is still pending, yet the hero and both calls to action are on screen.
      expect(TestBed.inject(PlayerSession).status()).toBe('restoring');
      expect(host.querySelector('h1')?.textContent?.trim()).toBe('Welcome aboard, Captain');
      expect(host.querySelectorAll('.hero__actions a').length).toBe(2);

      req.flush({
        accessToken: 'access-1',
        accessTokenExpiresAt: '2999-01-01T00:00:00Z',
        refreshToken: 'refresh-1',
        refreshTokenExpiresAt: '2999-01-01T00:00:00Z',
      });
      await fixture.whenStable();
      expect(TestBed.inject(PlayerSession).status()).toBe('known');
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
    });
  });
});
