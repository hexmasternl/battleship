import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { PlayersApi } from './players-api';
import { TokenPair } from './token-storage';

const PAIR: TokenPair = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2026-10-13T10:15:00Z',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2027-01-11T10:00:00Z',
};

describe('PlayersApi', () => {
  let api: PlayersApi;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(PlayersApi);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('creates tokens with a POST of the display name to /api/users/tokens', async () => {
    const result = firstValueFrom(api.createTokens('Admiral Nelson'));

    const req = backend.expectOne('/api/users/tokens');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ displayName: 'Admiral Nelson' });
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(PAIR, { status: 201, statusText: 'Created' });

    expect(await result).toEqual(PAIR);
  });

  it('refreshes tokens with a POST of the refresh token to /api/users/tokens/refresh', async () => {
    const result = firstValueFrom(api.refreshTokens('refresh-0'));

    const req = backend.expectOne('/api/users/tokens/refresh');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-0' });
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(PAIR);

    expect(await result).toEqual(PAIR);
  });

  it('surfaces a refused refresh as a 401 HttpErrorResponse', async () => {
    const result = firstValueFrom(api.refreshTokens('stale'));

    backend
      .expectOne('/api/users/tokens/refresh')
      .flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });

    const error = await result.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect((error as HttpErrorResponse).status).toBe(401);
  });
});
