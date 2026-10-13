import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GAMES_URL, GamesApi } from './games-api';

describe('GamesApi', () => {
  let api: GamesApi;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(GamesApi);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('posts to /api/games without a body and resolves the created game', async () => {
    const created = api.createGame();

    const req = backend.expectOne(GAMES_URL);
    expect(GAMES_URL).toBe('/api/games');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(
      { gameId: 'b0c5', gameCode: 'K7Q2M9XA' },
      { status: 201, statusText: 'Created', headers: { Location: '/games/K7Q2M9XA' } },
    );

    expect(await created).toEqual({ gameId: 'b0c5', gameCode: 'K7Q2M9XA' });
  });

  it('rejects with the HTTP error', async () => {
    const created = api.createGame().catch((e: unknown) => e);

    backend.expectOne(GAMES_URL).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(((await created) as HttpErrorResponse).status).toBe(401);
  });
});
