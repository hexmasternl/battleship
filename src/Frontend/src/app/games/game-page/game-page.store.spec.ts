import { TestBed } from '@angular/core/testing';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, flushPromises, provideFakeGamesApi } from '../../../testing/fake-games-api';
import { GAME_CODE, ME_ID, gameView, opponent } from '../../../testing/game-views';
import { GamePageStore, isReloadEventFor } from './game-page.store';

describe('isReloadEventFor', () => {
  const event = (type: string, payload: unknown) =>
    ({ type, id: '1', payload }) as Parameters<typeof isReloadEventFor>[0];

  it.each(['game.joined.v1', 'game.player-ready.v1', 'game.started.v1'])(
    'accepts %s for the same code',
    (type) => {
      expect(isReloadEventFor(event(type, { gameCode: GAME_CODE }), GAME_CODE)).toBe(true);
    },
  );

  it('rejects events for another game', () => {
    expect(
      isReloadEventFor(event('game.player-ready.v1', { gameCode: 'ZZZZ9999' }), GAME_CODE),
    ).toBe(false);
  });

  it('rejects other event types and payloads without a code', () => {
    expect(isReloadEventFor(event('game.shot-fired.v1', { gameCode: GAME_CODE }), GAME_CODE)).toBe(
      false,
    );
    expect(isReloadEventFor(event('game.joined.v1', null), GAME_CODE)).toBe(false);
  });
});

describe('GamePageStore', () => {
  let api: FakeGamesApi;
  let events: FakeEventsService;
  let store: GamePageStore;

  beforeEach(() => {
    api = new FakeGamesApi();
    events = new FakeEventsService();
    TestBed.configureTestingModule({
      providers: [provideFakeGamesApi(api), provideFakeEvents(events), GamePageStore],
    });
    store = TestBed.inject(GamePageStore);
  });

  async function loaded(view = gameView()): Promise<void> {
    const done = store.load(GAME_CODE);
    api.last('getGame').resolve(view);
    await done;
  }

  it('is loading while the game is requested', () => {
    void store.load(GAME_CODE);
    expect(store.status()).toBe('loading');
    expect(store.code()).toBe(GAME_CODE);
    expect(api.last('getGame').args).toEqual([GAME_CODE]);
  });

  it('shows the loaded game and its phase, and connects the events for the caller', async () => {
    const view = gameView({ opponent: opponent() });
    await loaded(view);
    expect(store.status()).toBe('loaded');
    expect(store.game()).toEqual(view);
    expect(store.phase()).toBe('new');
    expect(store.me()?.playerId).toBe(ME_ID);
    expect(store.opponent()?.playerName).toBe('Bob');
    expect(events.connected).toEqual([ME_ID]);
  });

  it('is not found on a game-not-found answer', async () => {
    const done = store.load(GAME_CODE);
    api.last('getGame').reject({ kind: 'game-not-found', status: 404 });
    await done;
    expect(store.status()).toBe('not-found');
    expect(store.game()).toBeNull();
  });

  it('fails on any other error and loads again on retry', async () => {
    const done = store.load(GAME_CODE);
    api.last('getGame').reject({ kind: 'network', status: 0 });
    await done;
    expect(store.status()).toBe('failed');

    const retried = store.retry();
    expect(store.status()).toBe('loading');
    api.last('getGame').resolve(gameView());
    await retried;
    expect(store.status()).toBe('loaded');
    expect(api.of('getGame').length).toBe(2);
  });

  it('keeps the shown game when a reload fails', async () => {
    await loaded();
    const done = store.reload();
    api.last('getGame').reject({ kind: 'network', status: 0 });
    await done;
    expect(store.status()).toBe('loaded');
    expect(store.game()).toEqual(gameView());
  });

  it.each([
    ['started', 'started'],
    ['completed', 'completed'],
    ['abandoned', 'abandoned'],
  ] as const)('reads the %s phase', async (state, phase) => {
    await loaded(gameView({ state }));
    expect(store.phase()).toBe(phase);
  });

  it('coalesces reloads asked for while one is in flight into one more', async () => {
    await loaded();
    const first = store.reload();
    void store.reload();
    void store.reload();
    expect(api.of('getGame').length).toBe(2);

    api.last('getGame').resolve(gameView());
    await flushPromises();
    expect(api.of('getGame').length).toBe(3);

    api.last('getGame').resolve(gameView({ opponent: opponent() }));
    await first;
    expect(api.of('getGame').length).toBe(3);
    expect(store.opponent()?.playerName).toBe('Bob');
  });

  it.each(['game.joined.v1', 'game.player-ready.v1', 'game.started.v1'] as const)(
    'reloads on %s for this game',
    async (type) => {
      await loaded();
      events.emit(type, { gameCode: GAME_CODE });
      expect(api.of('getGame').length).toBe(2);
    },
  );

  it('ignores events for another game and other event types', async () => {
    await loaded();
    events.emit('game.player-ready.v1', { gameCode: 'ZZZZ9999' });
    events.emit('game.shot-fired.v1', { gameCode: GAME_CODE });
    expect(api.of('getGame').length).toBe(1);
  });

  it('reloads when the stream goes from reconnecting to open, not on a first open', async () => {
    await loaded();
    events.setState('connecting');
    TestBed.tick();
    events.setState('open');
    TestBed.tick();
    expect(api.of('getGame').length).toBe(1);

    events.setState('reconnecting');
    TestBed.tick();
    events.setState('open');
    TestBed.tick();
    expect(api.of('getGame').length).toBe(2);
  });
});
