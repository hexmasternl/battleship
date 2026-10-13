import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ShipView } from '../game-view';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, flushPromises, provideFakeGamesApi } from '../../../testing/fake-games-api';
import {
  FULL_FLEET,
  GAME_CODE,
  ME_ID,
  gameView,
  me,
  opponent,
  ship,
} from '../../../testing/game-views';
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

describe('GamePageStore placement', () => {
  let api: FakeGamesApi;
  let store: GamePageStore;
  let snackBar: { open: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    api = new FakeGamesApi();
    snackBar = { open: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideFakeGamesApi(api),
        provideFakeEvents(),
        { provide: MatSnackBar, useValue: snackBar },
        GamePageStore,
      ],
    });
    store = TestBed.inject(GamePageStore);
  });

  async function loaded(ships: readonly ShipView[] = [], ready = false): Promise<void> {
    const done = store.load(GAME_CODE);
    api.last('getGame').resolve(gameView({ me: me({ ships, ready }) }));
    await done;
  }

  const placeCalls = () => api.of('placeShip').map((call) => call.args);

  describe('drag and drop', () => {
    it('places a ship dragged from the list where it is dropped, horizontal', async () => {
      await loaded();
      store.dragStart({ kind: 'destroyer', orientation: 'horizontal', grabIndex: 0 });
      store.dragOver({ column: 2, row: 3 });
      expect(store.preview()).toEqual({ ship: ship('destroyer', 2, 3), valid: true });

      store.dragEnd();
      expect(store.preview()).toBeNull();
      expect(store.placedShips()).toEqual([ship('destroyer', 2, 3)]);
      expect(store.unplacedFleet().map((f) => f.kind)).not.toContain('destroyer');
      await flushPromises();
      expect(placeCalls()).toEqual([
        [GAME_CODE, 'destroyer', { column: 2, row: 3, orientation: 'horizontal' }],
      ]);
    });

    it('keeps the grabbed cell under the pointer', async () => {
      await loaded();
      store.dragStart({ kind: 'cruiser', orientation: 'horizontal', grabIndex: 1 });
      store.dragOver({ column: 3, row: 3 });
      expect(store.preview()?.ship).toEqual(ship('cruiser', 2, 3));

      store.dragStart({ kind: 'cruiser', orientation: 'vertical', grabIndex: 2 });
      store.dragOver({ column: 3, row: 3 });
      expect(store.preview()?.ship).toEqual(ship('cruiser', 3, 1, 'vertical'));
    });

    it('moves a ship on the board and keeps its orientation', async () => {
      await loaded([ship('cruiser', 0, 0, 'vertical')]);
      store.dragStart({ kind: 'cruiser', orientation: 'vertical', grabIndex: 0 });
      store.dragOver({ column: 5, row: 5 });
      store.dragEnd();
      expect(store.placedShips()).toEqual([ship('cruiser', 5, 5, 'vertical')]);
      await flushPromises();
      expect(placeCalls()).toEqual([
        [GAME_CODE, 'cruiser', { column: 5, row: 5, orientation: 'vertical' }],
      ]);
    });

    it('shows an overlapping drop as invalid and leaves the ship where it was', async () => {
      const before = [ship('carrier', 0, 0), ship('battleship', 0, 5)];
      await loaded(before);
      store.dragStart({ kind: 'battleship', orientation: 'horizontal', grabIndex: 0 });
      store.dragOver({ column: 2, row: 0 });
      expect(store.preview()).toEqual({ ship: ship('battleship', 2, 0), valid: false });

      store.dragEnd();
      expect(store.placedShips()).toEqual(before);
      expect(placeCalls()).toEqual([]);
    });

    it('shows a drop past the edge as invalid and does nothing off the board', async () => {
      await loaded();
      store.dragStart({ kind: 'carrier', orientation: 'horizontal', grabIndex: 0 });
      store.dragOver({ column: 9, row: 0 });
      expect(store.preview()?.valid).toBe(false);
      store.dragOver(null);
      expect(store.preview()).toBeNull();
      store.dragEnd();
      expect(store.placedShips()).toEqual([]);
      expect(placeCalls()).toEqual([]);
    });
  });

  describe('tap and keyboard', () => {
    it('places the active ship with its origin on the activated cell, horizontal', async () => {
      await loaded();
      store.activate('submarine');
      store.cellActivated({ column: 5, row: 5 });
      expect(store.placedShips()).toEqual([ship('submarine', 5, 5)]);
      await flushPromises();
      expect(placeCalls().length).toBe(1);
    });

    it('activates a placed ship from its cell and moves it keeping its orientation', async () => {
      await loaded([ship('cruiser', 4, 4, 'vertical')]);
      store.cellActivated({ column: 4, row: 6 });
      expect(store.activeKind()).toBe('cruiser');
      expect(placeCalls()).toEqual([]);

      store.cellActivated({ column: 0, row: 0 });
      expect(store.placedShips()).toEqual([ship('cruiser', 0, 0, 'vertical')]);
    });

    it('does nothing on an empty cell without an active ship', async () => {
      await loaded();
      store.cellActivated({ column: 0, row: 0 });
      expect(store.activeKind()).toBeNull();
      expect(placeCalls()).toEqual([]);
    });

    it('refuses a tap placement that would leave the board, with a message', async () => {
      await loaded();
      store.activate('carrier');
      store.cellActivated({ column: 10, row: 0 });
      expect(store.placedShips()).toEqual([]);
      expect(placeCalls()).toEqual([]);
      expect(snackBar.open).toHaveBeenCalledWith(
        'The carrier would leave the board',
        'Dismiss',
        expect.anything(),
      );
    });

    it('clears the active ship', async () => {
      await loaded();
      store.activate('carrier');
      store.clearActive();
      expect(store.activeKind()).toBeNull();
    });

    it('ignores selection, drags and placement once the player is ready', async () => {
      await loaded(FULL_FLEET, true);
      expect(store.canEdit()).toBe(false);
      store.activate('carrier');
      expect(store.activeKind()).toBeNull();
      store.dragStart({ kind: 'carrier', orientation: 'horizontal', grabIndex: 0 });
      store.dragOver({ column: 0, row: 11 });
      store.dragEnd();
      expect(store.place(ship('carrier', 0, 11))).toBe(false);
      expect(placeCalls()).toEqual([]);
    });
  });
});
