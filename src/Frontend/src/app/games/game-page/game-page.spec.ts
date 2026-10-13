import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { Router, provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, flushPromises, provideFakeGamesApi } from '../../../testing/fake-games-api';
import { GAME_CODE, ME_ID, OPPONENT_ID, gameView, me, opponent } from '../../../testing/game-views';
import { GAME_RESULT_CLOSED } from '../game-result-dialog/game-result-dialog';
import { GameView } from '../game-view';
import { GamePage } from './game-page';
import { GamePageStore } from './game-page.store';

describe('GamePage', () => {
  let api: FakeGamesApi;
  let events: FakeEventsService;

  afterEach(async () => {
    // The result dialog closes only with its result; close any a test left open so none leaks.
    TestBed.inject(MatDialog).openDialogs.forEach((ref) => ref.close(GAME_RESULT_CLOSED));
    await flushPromises();
  });

  async function render() {
    api = new FakeGamesApi();
    events = new FakeEventsService();
    TestBed.configureTestingModule({
      imports: [GamePage],
      providers: [
        provideRouter([]),
        provideFakeGamesApi(api),
        provideFakeEvents(events),
        // jsdom fires no transition events, so dialogs and tabs settle only without animations.
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    const fixture = TestBed.createComponent(GamePage);
    fixture.componentRef.setInput('gameCode', GAME_CODE);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await flushPromises();
      await fixture.whenStable();
    };
    const answer = async (view: GameView) => {
      api.last('getGame').resolve(view);
      await settle();
    };
    const heading = () => host.querySelector('h2')?.textContent?.trim();
    return { fixture, host, settle, answer, heading };
  }

  it('requests the game for the code and shows a loading indicator', async () => {
    const { host } = await render();
    expect(api.last('getGame').args).toEqual([GAME_CODE]);
    expect(host.querySelector('h1')?.textContent?.trim()).toBe(GAME_CODE);
    expect(host.querySelector('[role="status"] mat-progress-spinner')).not.toBeNull();
  });

  it('shows the placement phase for a new game', async () => {
    const { answer, heading } = await render();
    await answer(gameView());
    expect(heading()).toBe('Place your fleet');
  });

  it('shows the game play view and no placement for a started game', async () => {
    const { host, answer, heading } = await render();
    await answer(gameView({ state: 'started' }));
    expect(heading()).toBe('Battle');
    expect(host.querySelector('battle-game-play-phase')).not.toBeNull();
    expect(host.textContent).not.toContain('Place your fleet');
  });

  it('shows game over with a link home for an abandoned game', async () => {
    const { host, answer, heading } = await render();
    await answer(gameView({ state: 'abandoned' }));
    expect(heading()).toBe('Game over');
    expect(host.querySelector('a')?.getAttribute('href')).toBe('/');
  });

  it('shows the final boards read only for a completed game', async () => {
    const { host, answer, heading } = await render();
    await answer(
      gameView({
        state: 'completed',
        winnerId: OPPONENT_ID,
        me: me({ ready: true }),
        opponent: opponent({ ready: true }),
      }),
    );
    expect(heading()).toBe('Battle');
    expect(host.querySelector('battle-game-play-phase')).not.toBeNull();
    expect(host.querySelector('.play__turn')).toBeNull();
  });

  it('keeps the same boards, now read only, when the game ends during play', async () => {
    const { fixture, host, answer } = await render();
    await answer(gameView({ state: 'started', me: me({ hasTurn: true }), opponent: opponent() }));
    const boards = host.querySelector('battle-game-play-phase');

    events.emit('game.ended.v1', { gameCode: GAME_CODE, reason: 'completed', winnerId: ME_ID });
    await fixture.whenStable();

    expect(host.querySelector('battle-game-play-phase')).toBe(boards);
    expect(host.querySelector('.play__turn')).toBeNull();
  });

  it('says the game was not found and links home on a 404', async () => {
    const { host, settle, heading } = await render();
    api.last('getGame').reject({ kind: 'game-not-found', status: 404 });
    await settle();
    expect(heading()).toBe('Game not found');
    expect(host.querySelector('a')?.getAttribute('href')).toBe('/');
  });

  it('shows an error with a retry that requests the game again', async () => {
    const { host, settle, answer, heading } = await render();
    api.last('getGame').reject({ kind: 'network', status: 0 });
    await settle();
    expect(heading()).toBe('The game could not be loaded');

    (host.querySelector('button') as HTMLButtonElement).click();
    await settle();
    expect(api.of('getGame').length).toBe(2);
    await answer(gameView());
    expect(heading()).toBe('Place your fleet');
  });

  it('switches to the game play view with the turn after a game started event', async () => {
    const { host, answer, heading } = await render();
    await answer(gameView());
    events.emit('game.started.v1', { gameCode: GAME_CODE });
    await answer(gameView({ state: 'started', me: me({ hasTurn: true }), opponent: opponent() }));
    expect(heading()).toBe('Battle');
    expect(host.querySelector('[role="status"]')?.textContent?.trim()).toBe('Your turn');
  });

  it('draws an opponent shot from the event stream on the Opponent board', async () => {
    const { fixture, host, answer } = await render();
    await answer(gameView({ state: 'started', me: me({ hasTurn: false }), opponent: opponent() }));
    events.emit('game.shot-fired.v1', {
      gameCode: GAME_CODE,
      playerId: OPPONENT_ID,
      column: 1,
      row: 1,
      hit: false,
    });
    events.emit('game.shot-fired.v1', {
      gameCode: 'ZZZZ9999',
      playerId: OPPONENT_ID,
      column: 2,
      row: 2,
      hit: true,
    });
    await fixture.whenStable();
    expect(
      host.querySelector('[role="cell"][aria-label="B2, miss"] battle-shot-marker'),
    ).not.toBeNull();
    expect(host.querySelector('[role="cell"][aria-label="C3, hit"]')).toBeNull();
  });

  describe('result dialog', () => {
    const LAST_CELL = { column: 4, row: 6 };

    const dialogs = () => document.querySelectorAll('mat-dialog-container');
    const dialogTitle = () =>
      document.querySelector('mat-dialog-container [mat-dialog-title]')?.textContent?.trim();
    const closeButton = () =>
      document.querySelector('mat-dialog-container button') as HTMLButtonElement | null;

    /** Renders the page, spying on navigation, and loads `view`. */
    async function rendered(view: GameView) {
      const page = await render();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      await page.answer(view);
      const store = page.fixture.debugElement.injector.get(GamePageStore);
      return { ...page, navigate, store };
    }

    const playing = () =>
      gameView({
        state: 'started',
        me: me({ ready: true, hasTurn: true }),
        opponent: opponent({ ready: true }),
      });

    it('opens over the final boards of a completed game that is loaded', async () => {
      const { host } = await rendered(
        gameView({
          state: 'completed',
          winnerId: OPPONENT_ID,
          me: me({ ready: true }),
          opponent: opponent({ ready: true, shots: [{ column: 1, row: 1, hit: true }] }),
        }),
      );
      expect(dialogs().length).toBe(1);
      expect(dialogTitle()).toBe('You lost');
      expect(host.querySelector('battle-game-play-phase')).not.toBeNull();
      expect(document.activeElement).toBe(closeButton());
    });

    it('opens on game.ended.v1 for this game only', async () => {
      const { settle } = await rendered(playing());
      events.emit('game.ended.v1', { gameCode: 'ZZZZ9999', reason: 'completed', winnerId: ME_ID });
      await settle();
      expect(dialogs().length).toBe(0);

      events.emit('game.ended.v1', { gameCode: GAME_CODE, reason: 'completed', winnerId: ME_ID });
      await settle();
      expect(dialogs().length).toBe(1);
      expect(dialogTitle()).toBe('You won');
    });

    it('opens a neutral result for an end without a winner', async () => {
      const { settle } = await rendered(playing());
      events.emit('game.ended.v1', { gameCode: GAME_CODE, reason: 'completed' });
      await settle();
      expect(dialogTitle()).toBe('The game has ended');
    });

    it('opens exactly one dialog when the winning response and the end event both arrive', async () => {
      const { store, settle, answer } = await rendered(playing());
      store.selectTarget(LAST_CELL);
      const fired = store.fireAtTarget();
      api.last('fireShot').resolve({ ...LAST_CELL, hit: true, state: 'completed' });
      await fired;
      await settle();
      expect(dialogs().length).toBe(1);
      expect(dialogTitle()).toBe('You won');

      events.emit('game.ended.v1', { gameCode: GAME_CODE, reason: 'completed', winnerId: ME_ID });
      await settle();
      await answer(
        gameView({
          state: 'completed',
          winnerId: ME_ID,
          me: me({ ready: true, shots: [{ ...LAST_CELL, hit: true }] }),
          opponent: opponent({ ready: true }),
        }),
      );
      expect(dialogs().length).toBe(1);
      expect(TestBed.inject(MatDialog).openDialogs.length).toBe(1);
    });

    it('goes home when Close is activated', async () => {
      const { navigate, settle } = await rendered(playing());
      events.emit('game.ended.v1', { gameCode: GAME_CODE, reason: 'completed', winnerId: ME_ID });
      await settle();
      expect(navigate).not.toHaveBeenCalled();

      closeButton()?.click();
      await settle();

      expect(dialogs().length).toBe(0);
      expect(navigate).toHaveBeenCalledExactlyOnceWith('/');
    });

    it('goes home when the dialog is dismissed with Escape', async () => {
      const { fixture, navigate, settle } = await rendered(playing());
      events.emit('game.ended.v1', { gameCode: GAME_CODE, reason: 'completed', winnerId: ME_ID });
      await settle();

      const loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
      await (await loader.getHarness(MatDialogHarness)).close();
      await settle();

      expect(dialogs().length).toBe(0);
      expect(navigate).toHaveBeenCalledExactlyOnceWith('/');
    });

    it('stays open and stays put on a click on the backdrop', async () => {
      const { navigate, settle } = await rendered(playing());
      events.emit('game.ended.v1', { gameCode: GAME_CODE, reason: 'completed', winnerId: ME_ID });
      await settle();

      (document.querySelector('.cdk-overlay-backdrop') as HTMLElement).click();
      await settle();

      expect(dialogs().length).toBe(1);
      expect(navigate).not.toHaveBeenCalled();
    });
  });

  it('has no accessibility violations while loading and once loaded', async () => {
    const { host, answer } = await render();
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
      await answer(gameView({ state: 'started' }));
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 20000);
});
