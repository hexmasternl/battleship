import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GAMES_URL, GamesApi, JoinGameFailure, joinGameUrl, toJoinGameError } from './games-api';

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

  describe('joinGame', () => {
    const JOIN_URL = '/api/games/K7Q2M9XA/join';
    const problem = (slug: string) => ({
      type: `https://battleship.hexmaster.nl/problems/${slug}`,
      title: 'Refused',
      status: 409,
    });

    /** Starts a join and resolves the reason it was refused with. */
    function refusal(): Promise<unknown> {
      return api.joinGame('K7Q2M9XA').then(
        () => {
          throw new Error('Expected the join to be refused');
        },
        (e: unknown) => (e instanceof JoinGameFailure ? e.reason : e),
      );
    }

    it('posts to /api/games/{code}/join without a body and resolves the joined game', async () => {
      const joined = api.joinGame('K7Q2M9XA');

      const req = backend.expectOne(JOIN_URL);
      expect(joinGameUrl('K7Q2M9XA')).toBe(JOIN_URL);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toBeNull();
      req.flush({ gameId: 'b0c5', gameCode: 'K7Q2M9XA' });

      expect(await joined).toEqual({ gameId: 'b0c5', gameCode: 'K7Q2M9XA' });
    });

    it('maps 404 to not-found', async () => {
      const reason = refusal();
      backend
        .expectOne(JOIN_URL)
        .flush(
          { type: 'https://battleship.hexmaster.nl/problems/game-not-found', status: 404 },
          { status: 404, statusText: 'Not Found' },
        );
      expect(await reason).toBe('not-found');
    });

    it.each([
      ['game-full', 'full'],
      ['game-already-started', 'started'],
      ['game-update-conflict', 'failed'],
      ['something-new', 'failed'],
    ] as const)('maps 409 %s to %s', async (slug, expected) => {
      const reason = refusal();
      backend.expectOne(JOIN_URL).flush(problem(slug), { status: 409, statusText: 'Conflict' });
      expect(await reason).toBe(expected);
    });

    it('maps a 409 without problem details to failed', async () => {
      const reason = refusal();
      backend.expectOne(JOIN_URL).flush(null, { status: 409, statusText: 'Conflict' });
      expect(await reason).toBe('failed');
    });

    it.each([401, 500, 503])('maps %d to failed', async (status) => {
      const reason = refusal();
      backend.expectOne(JOIN_URL).flush(null, { status, statusText: 'Error' });
      expect(await reason).toBe('failed');
    });

    it('maps a network error to failed', async () => {
      const reason = refusal();
      backend.expectOne(JOIN_URL).error(new ProgressEvent('error'));
      expect(await reason).toBe('failed');
    });

    it('maps anything that is not an HTTP error to failed', () => {
      expect(toJoinGameError(new Error('boom'))).toBe('failed');
    });
  });
});
