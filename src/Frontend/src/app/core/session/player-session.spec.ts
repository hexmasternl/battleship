import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MemoryStorage } from '../../../testing/memory-storage';
import { PlayerSession, SESSION_CLOCK, isExpired } from './player-session';
import {
  ACCESS_TOKEN_KEY,
  PERSISTENT_STORAGE,
  REFRESH_TOKEN_EXPIRES_AT_KEY,
  REFRESH_TOKEN_KEY,
  SESSION_STORAGE,
  TokenPair,
} from './token-storage';

const NOW = Date.parse('2026-10-13T10:00:00Z');
const REFRESH_URL = '/api/users/tokens/refresh';
const CREATE_URL = '/api/users/tokens';

function pair(n: number): TokenPair {
  return {
    accessToken: `access-${n}`,
    accessTokenExpiresAt: '2026-10-13T10:15:00Z',
    refreshToken: `refresh-${n}`,
    refreshTokenExpiresAt: '2027-01-11T10:00:00Z',
  };
}

/** Lets awaited promise chains inside the store settle before the next assertion. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('PlayerSession', () => {
  let local: MemoryStorage;
  let session: MemoryStorage;
  let store: PlayerSession;
  let backend: HttpTestingController;

  function storeRefreshToken(value: string, expiresAt = '2027-01-11T10:00:00Z'): void {
    local.setItem(REFRESH_TOKEN_KEY, value);
    local.setItem(REFRESH_TOKEN_EXPIRES_AT_KEY, expiresAt);
  }

  beforeEach(() => {
    local = new MemoryStorage();
    session = new MemoryStorage();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PERSISTENT_STORAGE, useValue: () => local },
        { provide: SESSION_STORAGE, useValue: () => session },
        { provide: SESSION_CLOCK, useValue: () => NOW },
      ],
    });
    store = TestBed.inject(PlayerSession);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('starts unknown', () => {
    expect(store.status()).toBe('unknown');
    expect(store.isKnown()).toBe(false);
  });

  describe('restore', () => {
    it('sends no request and stays unknown when no refresh token is stored', async () => {
      await store.restore();

      backend.expectNone(REFRESH_URL);
      expect(store.status()).toBe('unknown');
    });

    it('clears a locally expired token without a request and stays unknown', async () => {
      storeRefreshToken('refresh-0', '2026-10-13T09:59:59Z');
      session.setItem(ACCESS_TOKEN_KEY, 'access-0');

      await store.restore();

      backend.expectNone(REFRESH_URL);
      expect(store.status()).toBe('unknown');
      expect(local.keys()).toEqual([]);
      expect(session.keys()).toEqual([]);
    });

    it('stores the rotated pair and becomes known after a successful refresh', async () => {
      storeRefreshToken('refresh-0');

      const done = store.restore();
      expect(store.status()).toBe('restoring');

      const req = backend.expectOne(REFRESH_URL);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ refreshToken: 'refresh-0' });
      req.flush(pair(1));
      await done;

      expect(store.status()).toBe('known');
      expect(store.isKnown()).toBe(true);
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
      expect(local.getItem(REFRESH_TOKEN_EXPIRES_AT_KEY)).toBe('2027-01-11T10:00:00Z');
      expect(session.getItem(ACCESS_TOKEN_KEY)).toBe('access-1');
    });

    it('clears the tokens and becomes unknown on a 401 for the stored token', async () => {
      storeRefreshToken('refresh-0');
      session.setItem(ACCESS_TOKEN_KEY, 'access-0');

      const done = store.restore();
      backend.expectOne(REFRESH_URL).flush(null, { status: 401, statusText: 'Unauthorized' });
      await done;

      expect(store.status()).toBe('unknown');
      expect(local.keys()).toEqual([]);
      expect(session.keys()).toEqual([]);
    });

    it('retries once with the new value when another tab rotated the token before the 401', async () => {
      storeRefreshToken('refresh-0');

      const done = store.restore();
      const first = backend.expectOne(REFRESH_URL);
      // The other tab finished its refresh first and stored the rotated token.
      storeRefreshToken('refresh-other');
      first.flush(null, { status: 401, statusText: 'Unauthorized' });
      await settle();

      const retry = backend.expectOne(REFRESH_URL);
      expect(retry.request.body).toEqual({ refreshToken: 'refresh-other' });
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-other');
      retry.flush(pair(2));
      await done;

      expect(store.status()).toBe('known');
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-2');
    });

    it('retries only once and then clears when the retried token is refused too', async () => {
      storeRefreshToken('refresh-0');

      const done = store.restore();
      const first = backend.expectOne(REFRESH_URL);
      storeRefreshToken('refresh-other');
      first.flush(null, { status: 401, statusText: 'Unauthorized' });
      await settle();
      backend.expectOne(REFRESH_URL).flush(null, { status: 401, statusText: 'Unauthorized' });
      await done;

      backend.expectNone(REFRESH_URL);
      expect(store.status()).toBe('unknown');
      expect(local.keys()).toEqual([]);
    });

    it('keeps the refresh token and becomes unknown on a network failure', async () => {
      storeRefreshToken('refresh-0');

      const done = store.restore();
      backend.expectOne(REFRESH_URL).error(new ProgressEvent('error'));
      await done;

      expect(store.status()).toBe('unknown');
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-0');
    });

    it('keeps the refresh token and becomes unknown on a server error', async () => {
      storeRefreshToken('refresh-0');

      const done = store.restore();
      backend.expectOne(REFRESH_URL).flush(null, { status: 503, statusText: 'Unavailable' });
      await done;

      expect(store.status()).toBe('unknown');
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-0');
    });

    it('shares one request between concurrent calls', async () => {
      storeRefreshToken('refresh-0');

      const first = store.restore();
      const second = store.restore();
      expect(second).toBe(first);

      backend.expectOne(REFRESH_URL).flush(pair(1));
      await Promise.all([first, second]);

      expect(store.status()).toBe('known');
    });

    it('sends a new request once the previous one has finished', async () => {
      storeRefreshToken('refresh-0');
      const first = store.restore();
      backend.expectOne(REFRESH_URL).flush(pair(1));
      await first;

      const second = store.restore();
      const req = backend.expectOne(REFRESH_URL);
      expect(req.request.body).toEqual({ refreshToken: 'refresh-1' });
      req.flush(pair(2));
      await second;

      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-2');
    });
  });

  describe('createPlayer', () => {
    it('posts the display name, stores the pair and becomes known', async () => {
      const done = store.createPlayer('Admiral Nelson');

      const req = backend.expectOne(CREATE_URL);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ displayName: 'Admiral Nelson' });
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush(pair(1), { status: 201, statusText: 'Created' });
      await done;

      expect(store.isKnown()).toBe(true);
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
      expect(session.getItem(ACCESS_TOKEN_KEY)).toBe('access-1');
    });

    it('rejects, stores nothing and stays unknown when creation fails', async () => {
      const done = store.createPlayer('Admiral Nelson');
      backend.expectOne(CREATE_URL).flush(null, { status: 500, statusText: 'Server Error' });

      await expect(done).rejects.toBeTruthy();
      expect(store.isKnown()).toBe(false);
      expect(local.keys()).toEqual([]);
      expect(session.keys()).toEqual([]);
    });
  });

  describe('ensurePlayer', () => {
    it('reports false without a request when nothing is stored', async () => {
      expect(await store.ensurePlayer()).toBe(false);
      backend.expectNone(REFRESH_URL);
    });

    it('reports true without a request once the player is known', async () => {
      const created = store.createPlayer('Admiral Nelson');
      backend.expectOne(CREATE_URL).flush(pair(1));
      await created;

      expect(await store.ensurePlayer()).toBe(true);
      backend.expectNone(REFRESH_URL);
    });

    it('waits for a restore in flight', async () => {
      storeRefreshToken('refresh-0');
      void store.restore();

      const answer = store.ensurePlayer();
      backend.expectOne(REFRESH_URL).flush(pair(1));

      expect(await answer).toBe(true);
    });

    it('retries the refresh when an earlier one failed on the network', async () => {
      storeRefreshToken('refresh-0');
      const first = store.restore();
      backend.expectOne(REFRESH_URL).error(new ProgressEvent('error'));
      await first;

      const answer = store.ensurePlayer();
      backend.expectOne(REFRESH_URL).flush(pair(1));

      expect(await answer).toBe(true);
    });
  });
});

describe('isExpired', () => {
  it('compares the expiry instant with now', () => {
    expect(isExpired({ value: 'x', expiresAt: '2026-10-13T10:00:01Z' }, NOW)).toBe(false);
    expect(isExpired({ value: 'x', expiresAt: '2026-10-13T10:00:00Z' }, NOW)).toBe(true);
  });

  it('treats an unreadable instant as expired', () => {
    expect(isExpired({ value: 'x', expiresAt: 'not a date' }, NOW)).toBe(true);
  });
});
