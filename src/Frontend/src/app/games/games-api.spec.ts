import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GAME_CODE, gameView, me, ship } from '../../testing/game-views';
import { GameApiError, gameApiErrorMessage, toGameApiError } from './game-api-error';
import {
  FireShotFailure,
  FireShotResponse,
  GAMES_URL,
  GamesApi,
  JoinGameFailure,
  fireShotErrorMessage,
  isFireShotConflict,
  joinGameUrl,
  toFireShotError,
  toJoinGameError,
} from './games-api';

const PROBLEMS = 'https://battleship.hexmaster.nl/problems';

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

  describe('getGame', () => {
    it('gets /api/games/{code} and resolves the view', async () => {
      const view = gameView({ me: me({ ships: [ship('carrier', 0, 0)] }) });
      const loaded = api.getGame(GAME_CODE);

      const req = backend.expectOne(`/api/games/${GAME_CODE}`);
      expect(req.request.method).toBe('GET');
      req.flush(view);

      expect(await loaded).toEqual(view);
    });

    it('normalises the casing of the state, ship kinds and orientations', async () => {
      const loaded = api.getGame(GAME_CODE);

      backend.expectOne(`/api/games/${GAME_CODE}`).flush({
        ...gameView(),
        state: 'Started',
        me: { ...me(), ships: [{ kind: 'Carrier', column: 1, row: 2, orientation: 'Vertical' }] },
      });

      const view = await loaded;
      expect(view.state).toBe('started');
      expect(view.me.ships).toEqual([ship('carrier', 1, 2, 'vertical')]);
    });
  });

  describe('placeShip', () => {
    it('puts the placement to /api/games/{code}/ships/{kind} and resolves the view', async () => {
      const view = gameView({ me: me({ ships: [ship('destroyer', 2, 3)] }) });
      const placed = api.placeShip(GAME_CODE, 'destroyer', ship('destroyer', 2, 3));

      const req = backend.expectOne(`/api/games/${GAME_CODE}/ships/destroyer`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ column: 2, row: 3, orientation: 'horizontal' });
      req.flush(view);

      expect(await placed).toEqual(view);
    });
  });

  describe('markReady', () => {
    it('posts to /api/games/{code}/ready without a body and resolves the view', async () => {
      const view = gameView({ me: me({ ready: true }) });
      const ready = api.markReady(GAME_CODE);

      const req = backend.expectOne(`/api/games/${GAME_CODE}/ready`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toBeNull();
      req.flush(view);

      expect(await ready).toEqual(view);
    });
  });

  describe('errors', () => {
    it.each([
      ['game-not-found', 404],
      ['placement-out-of-board', 400],
      ['placement-overlap', 400],
      ['player-ready', 409],
      ['game-already-started', 409],
      ['fleet-incomplete', 409],
      ['game-update-conflict', 409],
    ] as const)('maps the %s problem (%i) onto its kind', async (slug, status) => {
      const failed = api.markReady(GAME_CODE).catch((e: unknown) => e);

      backend
        .expectOne(`/api/games/${GAME_CODE}/ready`)
        .flush(
          { type: `${PROBLEMS}/${slug}`, title: 'Refused', status },
          { status, statusText: 'Refused' },
        );

      expect(await failed).toEqual({ kind: slug, status } satisfies GameApiError);
    });

    it('maps a network failure onto network', async () => {
      const failed = api.getGame(GAME_CODE).catch((e: unknown) => e);

      backend.expectOne(`/api/games/${GAME_CODE}`).error(new ProgressEvent('error'));

      expect(await failed).toEqual({ kind: 'network', status: 0 });
    });

    it('maps a 404 without a problem body onto game-not-found', async () => {
      const failed = api.getGame(GAME_CODE).catch((e: unknown) => e);

      backend
        .expectOne(`/api/games/${GAME_CODE}`)
        .flush(null, { status: 404, statusText: 'Not Found' });

      expect(await failed).toEqual({ kind: 'game-not-found', status: 404 });
    });

    it('maps a validation error and a server error onto unexpected', async () => {
      const invalid = api
        .placeShip(GAME_CODE, 'carrier', ship('carrier', 0, 0))
        .catch((e: unknown) => e);
      backend
        .expectOne(`/api/games/${GAME_CODE}/ships/carrier`)
        .flush(
          { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1', status: 400 },
          { status: 400, statusText: 'Bad Request' },
        );
      expect(await invalid).toEqual({ kind: 'unexpected', status: 400 });

      const crashed = api.getGame(GAME_CODE).catch((e: unknown) => e);
      backend
        .expectOne(`/api/games/${GAME_CODE}`)
        .flush('boom', { status: 500, statusText: 'Server Error' });
      expect(await crashed).toEqual({ kind: 'unexpected', status: 500 });
    });

    it('maps anything that is not an HTTP error onto unexpected', () => {
      expect(toGameApiError(new Error('boom'))).toEqual({ kind: 'unexpected', status: 0 });
    });

    it('has a message for every kind and for unknown errors', () => {
      expect(gameApiErrorMessage({ kind: 'placement-overlap', status: 400 })).toContain('overlap');
      expect(gameApiErrorMessage({ kind: 'network', status: 0 })).toContain('connection');
      expect(gameApiErrorMessage('nonsense')).toBe('Something went wrong. Please try again.');
    });
  });

  describe('fireShot', () => {
    const SHOTS_URL = `/api/games/${GAME_CODE}/shots`;

    /** Starts a shot and resolves the reason it was refused with. */
    function refusal(): Promise<unknown> {
      return api.fireShot(GAME_CODE, 4, 6).then(
        () => {
          throw new Error('Expected the shot to be refused');
        },
        (e: unknown) => (e instanceof FireShotFailure ? e.reason : e),
      );
    }

    it('posts the cell to /api/games/{code}/shots and resolves the result', async () => {
      const fired = api.fireShot(GAME_CODE, 4, 6);

      const req = backend.expectOne(SHOTS_URL);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ column: 4, row: 6 });
      req.flush({ column: 4, row: 6, hit: true });

      expect(await fired).toEqual({ column: 4, row: 6, hit: true } satisfies FireShotResponse);
    });

    it.each([
      ['game-not-started', 'game-not-started'],
      ['not-your-turn', 'not-your-turn'],
      ['cell-already-shot', 'cell-already-shot'],
      ['game-update-conflict', 'game-update-conflict'],
      ['something-new', 'failed'],
    ] as const)('maps 409 %s to %s', async (slug, expected) => {
      const reason = refusal();
      backend
        .expectOne(SHOTS_URL)
        .flush(
          { type: `${PROBLEMS}/${slug}`, title: 'Refused', status: 409 },
          { status: 409, statusText: 'Conflict' },
        );
      expect(await reason).toBe(expected);
    });

    it('maps a 409 without problem details to failed', async () => {
      const reason = refusal();
      backend.expectOne(SHOTS_URL).flush(null, { status: 409, statusText: 'Conflict' });
      expect(await reason).toBe('failed');
    });

    it('maps 404 to not-found', async () => {
      const reason = refusal();
      backend
        .expectOne(SHOTS_URL)
        .flush(
          { type: `${PROBLEMS}/game-not-found`, status: 404 },
          { status: 404, statusText: 'Not Found' },
        );
      expect(await reason).toBe('not-found');
    });

    it.each([400, 401, 500])('maps %d to failed', async (status) => {
      const reason = refusal();
      backend.expectOne(SHOTS_URL).flush(null, { status, statusText: 'Error' });
      expect(await reason).toBe('failed');
    });

    it('maps a network error to failed', async () => {
      const reason = refusal();
      backend.expectOne(SHOTS_URL).error(new ProgressEvent('error'));
      expect(await reason).toBe('failed');
    });

    it('maps anything that is not an HTTP error to failed', () => {
      expect(toFireShotError(new Error('boom'))).toBe('failed');
    });

    it('has a player-facing message for every reason and tells conflicts apart', () => {
      const reasons = [
        'game-not-started',
        'not-your-turn',
        'cell-already-shot',
        'game-update-conflict',
        'not-found',
        'failed',
      ] as const;
      for (const reason of reasons) expect(fireShotErrorMessage(reason)).not.toBe('');
      expect(fireShotErrorMessage('not-your-turn')).toBe('It is not your turn.');
      expect(fireShotErrorMessage('failed')).toContain('not fired');
      expect(reasons.filter(isFireShotConflict)).toEqual([
        'game-not-started',
        'not-your-turn',
        'cell-already-shot',
        'game-update-conflict',
      ]);
    });
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
