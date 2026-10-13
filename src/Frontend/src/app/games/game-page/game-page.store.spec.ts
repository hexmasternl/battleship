import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GameView, ShipView } from '../game-view';
import { FireShotFailure } from '../games-api';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, flushPromises, provideFakeGamesApi } from '../../../testing/fake-games-api';
import {
  FULL_FLEET,
  GAME_CODE,
  ME_ID,
  OPPONENT_ID,
  gameView,
  me,
  opponent,
  ship,
} from '../../../testing/game-views';
import { GamePageStore, isReloadEventFor, shotFiredFrom, turnChangedFrom } from './game-page.store';

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

describe('GamePageStore play', () => {
  let api: FakeGamesApi;
  let store: GamePageStore;

  const E7 = { column: 4, row: 6 };
  const F2 = { column: 5, row: 1 };

  beforeEach(() => {
    api = new FakeGamesApi();
    TestBed.configureTestingModule({
      providers: [
        provideFakeGamesApi(api),
        provideFakeEvents(),
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
        GamePageStore,
      ],
    });
    store = TestBed.inject(GamePageStore);
  });

  /** Loads a started game; by default the caller has the turn. */
  async function playing(overrides: Partial<GameView> = {}): Promise<void> {
    const done = store.load(GAME_CODE);
    api.last('getGame').resolve(
      gameView({
        state: 'started',
        me: me({ ready: true, hasTurn: true, ships: FULL_FLEET }),
        opponent: opponent({ ready: true }),
        ...overrides,
      }),
    );
    await done;
  }

  /** Selects `cell`, fires and answers the request with `hit`. */
  async function fired(cell = E7, hit = false): Promise<void> {
    store.selectTarget(cell);
    const done = store.fireAtTarget();
    api.last('fireShot').resolve({ ...cell, hit, state: 'started' });
    await done;
  }

  const fireCalls = () => api.of('fireShot').map((call) => call.args);

  describe('slices', () => {
    it('reads the turn and both players shots', async () => {
      const mine = [{ column: 2, row: 4, hit: true }];
      const theirs = [{ column: 0, row: 0, hit: true }];
      await playing({
        me: me({ hasTurn: true, shots: mine }),
        opponent: opponent({ shots: theirs }),
      });
      expect(store.myTurn()).toBe(true);
      expect(store.myShots()).toEqual(mine);
      expect(store.opponentShots()).toEqual(theirs);
      expect(store.canShoot()).toBe(true);
    });

    it('has no shots and no turn before a game is loaded', () => {
      expect(store.myTurn()).toBe(false);
      expect(store.myShots()).toEqual([]);
      expect(store.opponentShots()).toEqual([]);
      expect(store.canShoot()).toBe(false);
    });

    it('cannot shoot in a game that has not started', async () => {
      const done = store.load(GAME_CODE);
      api.last('getGame').resolve(gameView({ me: me({ hasTurn: true }) }));
      await done;
      expect(store.canShoot()).toBe(false);
      store.selectTarget(E7);
      expect(store.target()).toBeNull();
    });
  });

  describe('selection', () => {
    it('marks the selected cell as the target and names it', async () => {
      await playing();
      store.selectTarget(E7);
      expect(store.target()).toEqual(E7);
      expect(store.targetName()).toBe('E7');
    });

    it('moves the target to the next selected cell', async () => {
      await playing();
      store.selectTarget(E7);
      store.selectTarget(F2);
      expect(store.target()).toEqual(F2);
      expect(store.targetName()).toBe('F2');
    });

    it('ignores a selection without the turn', async () => {
      await playing({ me: me({ hasTurn: false }), opponent: opponent({ hasTurn: true }) });
      expect(store.canShoot()).toBe(false);
      store.selectTarget(E7);
      expect(store.target()).toBeNull();
    });

    it('ignores a cell already fired at', async () => {
      await playing({ me: me({ hasTurn: true, shots: [{ ...E7, hit: false }] }) });
      store.selectTarget(E7);
      expect(store.target()).toBeNull();
    });

    it('ignores a selection while a shot is in flight', async () => {
      await playing();
      store.selectTarget(E7);
      void store.fireAtTarget();
      expect(store.shotStatus()).toBe('firing');
      expect(store.canShoot()).toBe(false);
      store.selectTarget(F2);
      expect(store.target()).toEqual(E7);
    });

    it('clears the target and sends nothing on cancel', async () => {
      await playing();
      store.selectTarget(E7);
      store.clearTarget();
      expect(store.target()).toBeNull();
      expect(store.myTurn()).toBe(true);
      expect(fireCalls()).toEqual([]);
    });
  });

  describe('firing', () => {
    it('does nothing without a target', async () => {
      await playing();
      await store.fireAtTarget();
      expect(fireCalls()).toEqual([]);
    });

    it('sends one request per confirm, however often it is asked for', async () => {
      await playing();
      store.selectTarget(E7);
      void store.fireAtTarget();
      void store.fireAtTarget();
      expect(fireCalls()).toEqual([[GAME_CODE, 4, 6]]);
    });

    it('draws the recorded shot, clears the target and leaves the turn as it is', async () => {
      await playing();
      await fired(E7, true);
      expect(store.myShots()).toEqual([{ ...E7, hit: true }]);
      expect(store.target()).toBeNull();
      expect(store.myTurn()).toBe(true);
      expect(store.opponent()?.hasTurn).toBe(false);
    });

    it('accepts no further shot until a turn change gives the turn back', async () => {
      await playing();
      await fired(E7);
      expect(store.canShoot()).toBe(false);
      store.selectTarget(F2);
      expect(store.target()).toBeNull();

      store.applyTurnChanged(OPPONENT_ID);
      expect(store.canShoot()).toBe(false);
      store.applyTurnChanged(ME_ID);
      expect(store.canShoot()).toBe(true);
      store.selectTarget(F2);
      expect(store.target()).toEqual(F2);
    });

    it('frees the next shot when the turn moved while the request was in flight', async () => {
      await playing();
      store.selectTarget(E7);
      const done = store.fireAtTarget();
      store.applyShotFired({ playerId: ME_ID, ...E7, hit: false });
      store.applyTurnChanged(OPPONENT_ID);
      expect(store.shotStatus()).toBe('firing');
      api.last('fireShot').resolve({ ...E7, hit: false, state: 'started' });
      await done;
      expect(store.myShots()).toEqual([{ ...E7, hit: false }]);
      expect(store.shotStatus()).toBe('idle');

      store.applyTurnChanged(ME_ID);
      expect(store.canShoot()).toBe(true);
    });

    it.each(['failed', 'not-found'] as const)(
      'returns to idle without a marker and keeps the reason on %s, without a reload',
      async (reason) => {
        await playing();
        store.selectTarget(E7);
        const done = store.fireAtTarget();
        api.last('fireShot').reject(new FireShotFailure(reason));
        await done;
        expect(store.shotStatus()).toBe('idle');
        expect(store.shotError()).toBe(reason);
        expect(store.target()).toBeNull();
        expect(store.myShots()).toEqual([]);
        expect(store.canShoot()).toBe(true);
        expect(api.of('getGame').length).toBe(1);
      },
    );

    it('treats a rejection that is not a FireShotFailure as failed', async () => {
      await playing();
      store.selectTarget(E7);
      const done = store.fireAtTarget();
      api.last('fireShot').reject(new Error('boom'));
      await done;
      expect(store.shotError()).toBe('failed');
    });

    it('clears the reason once a new target is selected', async () => {
      await playing();
      store.selectTarget(E7);
      const done = store.fireAtTarget();
      api.last('fireShot').reject(new FireShotFailure('failed'));
      await done;
      store.selectTarget(F2);
      expect(store.shotError()).toBeNull();
    });

    it.each([
      'not-your-turn',
      'cell-already-shot',
      'game-not-started',
      'game-update-conflict',
    ] as const)(
      'reloads the game after a 409 %s and shows the turn the server reports',
      async (reason) => {
        await playing();
        store.selectTarget(E7);
        const done = store.fireAtTarget();
        api.last('fireShot').reject(new FireShotFailure(reason));
        await flushPromises();
        expect(store.shotError()).toBe(reason);
        expect(api.of('getGame').length).toBe(2);

        api.last('getGame').resolve(
          gameView({
            state: 'started',
            me: me({ hasTurn: false, ships: FULL_FLEET }),
            opponent: opponent({ hasTurn: true }),
          }),
        );
        await done;
        expect(store.myTurn()).toBe(false);
        expect(store.myShots()).toEqual([]);
        expect(store.shotStatus()).toBe('idle');
      },
    );
  });

  describe('fresh shots', () => {
    it('has none after a load, whatever shots the game holds', async () => {
      await playing({
        me: me({ hasTurn: true, shots: [{ ...F2, hit: false }] }),
        opponent: opponent({ shots: [{ column: 0, row: 0, hit: true }] }),
      });
      expect(store.myFreshShots()).toEqual([]);
      expect(store.opponentFreshShots()).toEqual([]);
    });

    it('marks a shot drawn from the response and from events as fresh, per shooter', async () => {
      await playing();
      await fired(E7, true);
      store.applyShotFired({ playerId: ME_ID, ...E7, hit: true });
      store.applyShotFired({ playerId: OPPONENT_ID, ...F2, hit: false });
      expect(store.myFreshShots()).toEqual([E7]);
      expect(store.opponentFreshShots()).toEqual([F2]);
    });

    it('marks a shot whose event arrived before the response once', async () => {
      await playing();
      store.selectTarget(E7);
      const done = store.fireAtTarget();
      store.applyShotFired({ playerId: ME_ID, ...E7, hit: false });
      api.last('fireShot').resolve({ ...E7, hit: false, state: 'started' });
      await done;
      expect(store.myFreshShots()).toEqual([E7]);
    });

    it('drops every fresh mark on a reload', async () => {
      await playing();
      store.applyShotFired({ playerId: OPPONENT_ID, ...F2, hit: false });
      const done = store.reload();
      api.last('getGame').resolve(
        gameView({
          state: 'started',
          me: me({ hasTurn: true, ships: FULL_FLEET }),
          opponent: opponent({ shots: [{ ...F2, hit: false }] }),
        }),
      );
      await done;
      expect(store.opponentShots()).toEqual([{ ...F2, hit: false }]);
      expect(store.opponentFreshShots()).toEqual([]);
    });
  });

  describe('events', () => {
    it('adds a shot of the opponent to their shots', async () => {
      await playing();
      store.applyShotFired({ playerId: OPPONENT_ID, column: 1, row: 1, hit: true });
      expect(store.opponentShots()).toEqual([{ column: 1, row: 1, hit: true }]);
      expect(store.myShots()).toEqual([]);
    });

    it('adds a shot of the caller to their shots', async () => {
      await playing();
      store.applyShotFired({ playerId: ME_ID, ...E7, hit: false });
      expect(store.myShots()).toEqual([{ ...E7, hit: false }]);
      expect(store.opponentShots()).toEqual([]);
    });

    it('does not add the echo of a shot drawn from the response, nor a replay', async () => {
      await playing();
      await fired(E7, true);
      store.applyShotFired({ playerId: ME_ID, ...E7, hit: true });
      store.applyShotFired({ playerId: OPPONENT_ID, ...F2, hit: false });
      store.applyShotFired({ playerId: OPPONENT_ID, ...F2, hit: false });
      expect(store.myShots()).toEqual([{ ...E7, hit: true }]);
      expect(store.opponentShots()).toEqual([{ ...F2, hit: false }]);
    });

    it('lets both players shoot at the same coordinates', async () => {
      await playing();
      store.applyShotFired({ playerId: ME_ID, ...E7, hit: false });
      store.applyShotFired({ playerId: OPPONENT_ID, ...E7, hit: true });
      expect(store.myShots()).toEqual([{ ...E7, hit: false }]);
      expect(store.opponentShots()).toEqual([{ ...E7, hit: true }]);
    });

    it('ignores a shot of an unknown player', async () => {
      await playing();
      const before = store.state();
      store.applyShotFired({ playerId: 'someone-else', ...E7, hit: true });
      expect(store.state()).toBe(before);
    });

    it('drops a target that an own shot event marks as shot', async () => {
      await playing();
      store.selectTarget(E7);
      store.applyShotFired({ playerId: ME_ID, ...E7, hit: false });
      expect(store.target()).toBeNull();
    });

    it('sets both turn flags from a turn change and drops a target without the turn', async () => {
      await playing();
      store.selectTarget(E7);
      store.applyTurnChanged(OPPONENT_ID);
      expect(store.myTurn()).toBe(false);
      expect(store.opponent()?.hasTurn).toBe(true);
      expect(store.target()).toBeNull();

      store.applyTurnChanged(ME_ID);
      expect(store.myTurn()).toBe(true);
      expect(store.opponent()?.hasTurn).toBe(false);
    });
  });
});

describe('shotFiredFrom and turnChangedFrom', () => {
  const event = (type: string, payload: unknown) =>
    ({ type, id: '1', payload }) as Parameters<typeof shotFiredFrom>[0];

  it('reads a shot fired payload', () => {
    expect(
      shotFiredFrom(
        event('game.shot-fired.v1', {
          gameId: 'g',
          gameCode: GAME_CODE,
          playerId: OPPONENT_ID,
          column: 1,
          row: 2,
          hit: true,
        }),
      ),
    ).toEqual({ playerId: OPPONENT_ID, column: 1, row: 2, hit: true });
  });

  it('rejects malformed shot payloads and other types', () => {
    expect(shotFiredFrom(event('game.shot-fired.v1', { playerId: 'p', column: 1 }))).toBeNull();
    expect(shotFiredFrom(event('game.shot-fired.v1', null))).toBeNull();
    expect(
      shotFiredFrom(event('game.started.v1', { playerId: 'p', column: 1, row: 1, hit: true })),
    ).toBeNull();
  });

  it('reads a turn changed payload and rejects anything else', () => {
    expect(turnChangedFrom(event('game.turn-changed.v1', { playerWithTurnId: ME_ID }))).toBe(ME_ID);
    expect(turnChangedFrom(event('game.turn-changed.v1', {}))).toBeNull();
    expect(turnChangedFrom(event('game.started.v1', { playerWithTurnId: ME_ID }))).toBeNull();
  });
});

describe('GamePageStore play events', () => {
  let api: FakeGamesApi;
  let events: FakeEventsService;
  let store: GamePageStore;

  beforeEach(async () => {
    api = new FakeGamesApi();
    events = new FakeEventsService();
    TestBed.configureTestingModule({
      providers: [provideFakeGamesApi(api), provideFakeEvents(events), GamePageStore],
    });
    store = TestBed.inject(GamePageStore);
    const done = store.load(GAME_CODE);
    api.last('getGame').resolve(
      gameView({
        state: 'started',
        me: me({ hasTurn: true, ships: FULL_FLEET }),
        opponent: opponent(),
      }),
    );
    await done;
  });

  const shot = (code: string, playerId: string, column: number, row: number, hit: boolean) =>
    events.emit('game.shot-fired.v1', {
      gameId: 'game-1',
      gameCode: code,
      playerId,
      column,
      row,
      hit,
    });
  const turn = (code: string, playerWithTurnId: string) =>
    events.emit('game.turn-changed.v1', { gameId: 'game-1', gameCode: code, playerWithTurnId });

  it('routes a shot of the opponent to their shots and the turn change to the turn', () => {
    shot(GAME_CODE, OPPONENT_ID, 1, 1, true);
    turn(GAME_CODE, ME_ID);
    expect(store.opponentShots()).toEqual([{ column: 1, row: 1, hit: true }]);
    expect(store.myTurn()).toBe(true);

    turn(GAME_CODE, OPPONENT_ID);
    expect(store.myTurn()).toBe(false);
    expect(store.opponent()?.hasTurn).toBe(true);
    expect(api.of('getGame').length).toBe(1);
  });

  it('routes the caller own shot event without duplicating it', () => {
    shot(GAME_CODE, ME_ID, 4, 6, false);
    shot(GAME_CODE, ME_ID, 4, 6, false);
    expect(store.myShots()).toEqual([{ column: 4, row: 6, hit: false }]);
  });

  it('matches the game code in any casing', () => {
    shot(GAME_CODE.toLowerCase(), OPPONENT_ID, 2, 2, false);
    expect(store.opponentShots()).toEqual([{ column: 2, row: 2, hit: false }]);
  });

  it('ignores shots and turn changes of another game', () => {
    const before = store.state();
    shot('ZZZZ9999', OPPONENT_ID, 1, 1, true);
    turn('ZZZZ9999', OPPONENT_ID);
    expect(store.state()).toBe(before);
    expect(api.of('getGame').length).toBe(1);
  });

  it('switches a placing page to play on game.started.v1 with the turn the server reports', async () => {
    const done = store.load(GAME_CODE);
    api.last('getGame').resolve(gameView({ me: me({ ready: true, ships: FULL_FLEET }) }));
    await done;
    expect(store.phase()).toBe('new');

    events.emit('game.started.v1', { gameId: 'game-1', gameCode: GAME_CODE });
    expect(api.of('getGame').length).toBe(3);
    api.last('getGame').resolve(
      gameView({
        state: 'started',
        me: me({ ready: true, hasTurn: true, ships: FULL_FLEET }),
        opponent: opponent({ ready: true }),
      }),
    );
    await flushPromises();
    expect(store.phase()).toBe('started');
    expect(store.myTurn()).toBe(true);
    expect(store.canShoot()).toBe(true);
  });

  it('reloads exactly once per reconnect and shows what was missed', async () => {
    events.setState('open');
    TestBed.tick();
    events.setState('reconnecting');
    TestBed.tick();
    events.setState('open');
    TestBed.tick();
    TestBed.tick();
    expect(api.of('getGame').length).toBe(2);

    api.last('getGame').resolve(
      gameView({
        state: 'started',
        me: me({ hasTurn: true, ships: FULL_FLEET }),
        opponent: opponent({ shots: [{ column: 0, row: 0, hit: true }] }),
      }),
    );
    await flushPromises();
    expect(api.of('getGame').length).toBe(2);
    expect(store.opponentShots()).toEqual([{ column: 0, row: 0, hit: true }]);
    expect(store.myTurn()).toBe(true);
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

  describe('rotation', () => {
    it('turns the active ship clockwise about its origin', async () => {
      await loaded([ship('cruiser', 1, 1)]);
      store.activate('cruiser');
      store.rotateActive();
      expect(store.placedShips()).toEqual([ship('cruiser', 1, 1, 'vertical')]);
      await flushPromises();
      expect(placeCalls()).toEqual([
        [GAME_CODE, 'cruiser', { column: 1, row: 1, orientation: 'vertical' }],
      ]);
    });

    it('refuses a rotation that would leave the board, with a message', async () => {
      await loaded([ship('carrier', 0, 9)]);
      store.activate('carrier');
      store.rotateActive();
      await flushPromises();
      expect(store.placedShips()).toEqual([ship('carrier', 0, 9)]);
      expect(placeCalls()).toEqual([]);
      expect(snackBar.open).toHaveBeenCalledWith(
        'The carrier would leave the board',
        'Dismiss',
        expect.anything(),
      );
    });

    it('refuses a rotation that would overlap another ship, with a message', async () => {
      await loaded([ship('cruiser', 0, 0), ship('battleship', 0, 1)]);
      store.activate('cruiser');
      store.rotateActive();
      await flushPromises();
      expect(store.placedShips()).toEqual([ship('cruiser', 0, 0), ship('battleship', 0, 1)]);
      expect(placeCalls()).toEqual([]);
      expect(snackBar.open).toHaveBeenCalledWith(
        'The cruiser would overlap the battleship',
        'Dismiss',
        expect.anything(),
      );
    });

    it('keeps one active ship at a time and offers rotation only for a placed one', async () => {
      await loaded([ship('cruiser', 0, 0)]);
      store.activate('cruiser');
      expect(store.activeShip()).toEqual({
        kind: 'cruiser',
        name: 'Cruiser',
        placed: true,
        rotateLabel: 'Rotate cruiser',
      });
      store.activate('destroyer');
      expect(store.activeKind()).toBe('destroyer');
      expect(store.activeShip()?.placed).toBe(false);
      store.rotateActive();
      expect(placeCalls()).toEqual([]);
    });
  });

  describe('saving', () => {
    it('sends one request per change, in order, one at a time', async () => {
      await loaded();
      store.place(ship('destroyer', 0, 0));
      store.place(ship('destroyer', 4, 4));
      store.place(ship('carrier', 0, 6));
      await flushPromises();
      expect(placeCalls().length).toBe(1);
      expect(store.pendingSaves()).toBe(3);

      api.last('placeShip').resolve(gameView({ me: me({ ships: [ship('destroyer', 0, 0)] }) }));
      await flushPromises();
      // An answer that predates later changes does not undo them on screen.
      expect(store.placedShips()).toEqual([ship('carrier', 0, 6), ship('destroyer', 4, 4)]);
      expect(placeCalls().length).toBe(2);

      api.last('placeShip').resolve(gameView({ me: me({ ships: [ship('destroyer', 4, 4)] }) }));
      await flushPromises();
      const final = [ship('carrier', 0, 6), ship('destroyer', 4, 4)];
      api.last('placeShip').resolve(gameView({ me: me({ ships: final }) }));
      await store.settled();

      expect(placeCalls().map(([, kind, at]) => [kind, at])).toEqual([
        ['destroyer', { column: 0, row: 0, orientation: 'horizontal' }],
        ['destroyer', { column: 4, row: 4, orientation: 'horizontal' }],
        ['carrier', { column: 0, row: 6, orientation: 'horizontal' }],
      ]);
      expect(store.pendingSaves()).toBe(0);
      expect(store.placedShips()).toEqual(final);
    });

    it('shows a message and reloads the server state after a 409', async () => {
      const server = [ship('carrier', 0, 0)];
      await loaded(server);
      store.place(ship('destroyer', 5, 5));
      await flushPromises();
      api.last('placeShip').reject({ kind: 'game-update-conflict', status: 409 });
      await flushPromises();

      expect(snackBar.open).toHaveBeenCalledWith(
        'The game changed at the same moment. Please try again.',
        'Dismiss',
        expect.anything(),
      );
      expect(api.of('getGame').length).toBe(2);
      api.last('getGame').resolve(gameView({ me: me({ ships: server }) }));
      await store.settled();
      expect(store.placedShips()).toEqual(server);
      expect(store.pendingSaves()).toBe(0);
    });
  });

  describe('ready', () => {
    it('is possible only with five ships placed and no save pending', async () => {
      await loaded(FULL_FLEET.slice(0, 4));
      expect(store.canReady()).toBe(false);

      store.place(FULL_FLEET[4]);
      expect(store.canReady()).toBe(false);
      await flushPromises();
      api.last('placeShip').resolve(gameView({ me: me({ ships: FULL_FLEET }) }));
      await store.settled();
      expect(store.canReady()).toBe(true);
    });

    it('sends one request however often it is asked for, then makes the board read only', async () => {
      await loaded(FULL_FLEET);
      store.activate('carrier');
      void store.markReady();
      void store.markReady();
      expect(store.readying()).toBe(true);
      expect(store.canEdit()).toBe(false);
      expect(store.activeKind()).toBeNull();
      expect(api.of('markReady').map((call) => call.args)).toEqual([[GAME_CODE]]);

      api.last('markReady').resolve(gameView({ me: me({ ships: FULL_FLEET, ready: true }) }));
      await flushPromises();
      expect(store.isReady()).toBe(true);
      expect(store.readying()).toBe(false);
      expect(store.canEdit()).toBe(false);
      expect(store.canReady()).toBe(false);
      void store.markReady();
      expect(api.of('markReady').length).toBe(1);
    });

    it('shows a message and reloads when ready is refused', async () => {
      await loaded(FULL_FLEET);
      const done = store.markReady();
      api.last('markReady').reject({ kind: 'fleet-incomplete', status: 409 });
      await flushPromises();
      expect(snackBar.open).toHaveBeenCalledWith(
        'Place all five ships before you declare ready.',
        'Dismiss',
        expect.anything(),
      );
      api.last('getGame').resolve(gameView({ me: me({ ships: FULL_FLEET.slice(0, 4) }) }));
      await done;
      expect(store.readying()).toBe(false);
      expect(store.placedShips().length).toBe(4);
    });

    it('is read only after a reload of a ready player', async () => {
      await loaded(FULL_FLEET, true);
      expect(store.isReady()).toBe(true);
      expect(store.canEdit()).toBe(false);
      expect(store.canReady()).toBe(false);
    });
  });
});
