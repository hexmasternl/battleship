import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { MemoryStorage } from '../../../testing/memory-storage';
import { apiGatewayInterceptor } from '../api-gateway.interceptor';
import { BackendConfig } from '../backend-config';
import { RETRIED_AFTER_RENEWAL, authInterceptor } from './auth.interceptor';
import { UserSession, SESSION_CLOCK } from './user-session';
import {
  ACCESS_TOKEN_EXPIRES_AT_KEY,
  ACCESS_TOKEN_KEY,
  PERSISTENT_STORAGE,
  REFRESH_TOKEN_EXPIRES_AT_KEY,
  REFRESH_TOKEN_KEY,
  SESSION_STORAGE,
  TokenPair,
} from './token-storage';

const GATEWAY = 'http://localhost:58947';
const NOW = Date.parse('2026-10-13T10:00:00Z');
const GAMES = `${GATEWAY}/games`;
const REFRESH = `${GATEWAY}/users/tokens/refresh`;
const CREATE_TOKENS = `${GATEWAY}/users/tokens`;
const CREATED = { gameId: 'b0c5', gameCode: 'K7Q2M9XA' };

const RENEWED: TokenPair = {
  accessToken: 'access-2',
  accessTokenExpiresAt: '2026-10-13T10:15:00Z',
  refreshToken: 'refresh-2',
  refreshTokenExpiresAt: '2027-01-11T10:00:00Z',
};

const unauthorized = { status: 401, statusText: 'Unauthorized' };

/** Lets awaited promise chains inside the interceptor settle before the next assertion. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('authInterceptor', () => {
  let local: MemoryStorage;
  let session: MemoryStorage;
  let http: HttpClient;
  let backend: HttpTestingController;

  function storeAccessToken(value: string, expiresAt: string): void {
    session.setItem(ACCESS_TOKEN_KEY, value);
    session.setItem(ACCESS_TOKEN_EXPIRES_AT_KEY, expiresAt);
  }

  function storeRefreshToken(value = 'refresh-1'): void {
    local.setItem(REFRESH_TOKEN_KEY, value);
    local.setItem(REFRESH_TOKEN_EXPIRES_AT_KEY, '2027-01-11T10:00:00Z');
  }

  const authorization = (req: { request: { headers: { get(name: string): string | null } } }) =>
    req.request.headers.get('Authorization');

  beforeEach(() => {
    local = new MemoryStorage();
    session = new MemoryStorage();
    TestBed.configureTestingModule({
      providers: [
        // The production order: authentication first, then the /api rewrite to the gateway.
        provideHttpClient(withInterceptors([authInterceptor, apiGatewayInterceptor])),
        provideHttpClientTesting(),
        { provide: BackendConfig, useValue: { baseUrl: signal(GATEWAY).asReadonly() } },
        { provide: PERSISTENT_STORAGE, useValue: () => local },
        { provide: SESSION_STORAGE, useValue: () => session },
        { provide: SESSION_CLOCK, useValue: () => NOW },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  describe('the bearer header', () => {
    it('is added to a backend request on its way to the gateway', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');
      storeRefreshToken();

      const result = firstValueFrom(http.post('/api/games', null));
      await settle();

      const req = backend.expectOne(GAMES);
      expect(authorization(req)).toBe('Bearer access-1');
      req.flush(CREATED, { status: 201, statusText: 'Created' });
      expect(await result).toEqual(CREATED);
    });

    it('is not added to the refresh request', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');
      storeRefreshToken();

      const restored = TestBed.inject(UserSession).restore();
      await settle();

      const req = backend.expectOne(REFRESH);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush(RENEWED);
      await restored;
    });

    it('is not added to the create-tokens request', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');

      const created = TestBed.inject(UserSession).createUser('Admiral Nelson');
      await settle();

      const req = backend.expectOne(CREATE_TOKENS);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush(RENEWED, { status: 201, statusText: 'Created' });
      await created;
    });

    it('is not added to requests outside /api/', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');

      http.get('/config.json').subscribe();
      http.get('https://example.com/api/games').subscribe();
      await settle();

      expect(backend.expectOne('/config.json').request.headers.has('Authorization')).toBe(false);
      expect(
        backend.expectOne('https://example.com/api/games').request.headers.has('Authorization'),
      ).toBe(false);
    });

    it('is left out when the session holds no tokens at all', async () => {
      http.post('/api/games', null).subscribe({ error: () => undefined });
      await settle();

      const req = backend.expectOne(GAMES);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush(null, unauthorized);
      await settle();
      backend.expectNone(REFRESH);
    });
  });

  describe('renewal before sending', () => {
    it.each([
      ['has expired', '2026-10-13T09:59:00Z'],
      ['expires within 30 seconds', '2026-10-13T10:00:20Z'],
    ])('renews first when the access token %s', async (_, expiresAt) => {
      storeAccessToken('access-1', expiresAt);
      storeRefreshToken();

      const result = firstValueFrom(http.post('/api/games', null));
      await settle();

      backend.expectNone(GAMES);
      backend.expectOne(REFRESH).flush(RENEWED);
      await settle();

      const req = backend.expectOne(GAMES);
      expect(authorization(req)).toBe('Bearer access-2');
      req.flush(CREATED, { status: 201, statusText: 'Created' });
      expect(await result).toEqual(CREATED);
    });

    it('renews first when only a refresh token is stored', async () => {
      storeRefreshToken();

      http.post('/api/games', null).subscribe();
      await settle();

      backend.expectOne(REFRESH).flush(RENEWED);
      await settle();
      expect(authorization(backend.expectOne(GAMES))).toBe('Bearer access-2');
    });

    it('sends a token that is still valid for more than 30 seconds without renewing', async () => {
      storeAccessToken('access-1', '2026-10-13T10:00:31Z');
      storeRefreshToken();

      http.post('/api/games', null).subscribe();
      await settle();

      backend.expectNone(REFRESH);
      expect(authorization(backend.expectOne(GAMES))).toBe('Bearer access-1');
    });

    it('shares one renewal between two concurrent requests', async () => {
      storeAccessToken('access-1', '2026-10-13T09:59:00Z');
      storeRefreshToken();

      http.post('/api/games', null).subscribe();
      http.get('/api/games/K7Q2M9XA').subscribe();
      await settle();

      backend.expectOne(REFRESH).flush(RENEWED);
      await settle();

      const create = backend.expectOne(GAMES);
      const read = backend.expectOne(`${GAMES}/K7Q2M9XA`);
      expect(authorization(create)).toBe('Bearer access-2');
      expect(authorization(read)).toBe('Bearer access-2');
    });
  });

  describe('a refused token', () => {
    it('is renewed once and the request repeated once with the new token', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');
      storeRefreshToken();

      const result = firstValueFrom(http.post('/api/games', null));
      await settle();

      backend.expectOne(GAMES).flush(null, unauthorized);
      await settle();
      backend.expectOne(REFRESH).flush(RENEWED);
      await settle();

      const repeat = backend.expectOne(GAMES);
      expect(authorization(repeat)).toBe('Bearer access-2');
      expect(repeat.request.context.get(RETRIED_AFTER_RENEWAL)).toBe(true);
      repeat.flush(CREATED, { status: 201, statusText: 'Created' });
      expect(await result).toEqual(CREATED);
    });

    it('passes a second 401 to the caller without another renewal or repeat', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');
      storeRefreshToken();

      const result = firstValueFrom(http.post('/api/games', null)).catch((e: unknown) => e);
      await settle();

      backend.expectOne(GAMES).flush(null, unauthorized);
      await settle();
      backend.expectOne(REFRESH).flush(RENEWED);
      await settle();
      backend.expectOne(GAMES).flush(null, unauthorized);
      await settle();

      backend.expectNone(REFRESH);
      backend.expectNone(GAMES);
      const error = await result;
      expect(error).toBeInstanceOf(HttpErrorResponse);
      expect((error as HttpErrorResponse).status).toBe(401);
    });

    it('passes the 401 to the caller and clears the session when the refresh is refused', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');
      storeRefreshToken();

      const result = firstValueFrom(http.post('/api/games', null)).catch((e: unknown) => e);
      await settle();

      backend.expectOne(GAMES).flush(null, unauthorized);
      await settle();
      backend.expectOne(REFRESH).flush(null, unauthorized);
      await settle();

      backend.expectNone(GAMES);
      const error = await result;
      expect((error as HttpErrorResponse).status).toBe(401);
      expect(local.getItem(REFRESH_TOKEN_KEY)).toBeNull();
      expect(TestBed.inject(UserSession).isKnown()).toBe(false);
    });

    it('is not renewed when the request carried no token', async () => {
      http.post('/api/games', null).subscribe({ error: () => undefined });
      await settle();

      backend.expectOne(GAMES).flush(null, unauthorized);
      await settle();

      backend.expectNone(REFRESH);
      backend.expectNone(GAMES);
    });

    it('passes other errors to the caller untouched', async () => {
      storeAccessToken('access-1', '2026-10-13T10:15:00Z');
      storeRefreshToken();

      const result = firstValueFrom(http.post('/api/games', null)).catch((e: unknown) => e);
      await settle();
      backend.expectOne(GAMES).flush(null, { status: 500, statusText: 'Server Error' });

      const error = await result;
      expect((error as HttpErrorResponse).status).toBe(500);
      backend.expectNone(REFRESH);
    });
  });
});
