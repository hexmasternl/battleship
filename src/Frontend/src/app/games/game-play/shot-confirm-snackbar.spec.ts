import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, flushPromises, provideFakeGamesApi } from '../../../testing/fake-games-api';
import {
  FULL_FLEET,
  GAME_CODE,
  OPPONENT_ID,
  gameView,
  me,
  opponent,
} from '../../../testing/game-views';
import { GamePageStore } from '../game-page/game-page.store';
import { GameView, ShotView } from '../game-view';
import { FireShotFailure } from '../games-api';
import { GamePlayPhase, SHOT_ERROR_DURATION } from './game-play-phase';
import { ShotConfirmSnackbar } from './shot-confirm-snackbar';

function started(myTurn: boolean, mine: readonly ShotView[] = []): GameView {
  return gameView({
    state: 'started',
    me: me({ ready: true, hasTurn: myTurn, ships: FULL_FLEET, shots: mine }),
    opponent: opponent({ ready: true, hasTurn: !myTurn }),
  });
}

describe('Shot confirmation', () => {
  async function render(view: GameView) {
    const api = new FakeGamesApi();
    TestBed.configureTestingModule({
      imports: [GamePlayPhase],
      providers: [
        provideFakeGamesApi(api),
        provideFakeEvents(new FakeEventsService()),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        GamePageStore,
      ],
    });
    const store = TestBed.inject(GamePageStore);
    const snackBar = TestBed.inject(MatSnackBar);
    const openFromComponent = vi.spyOn(snackBar, 'openFromComponent');
    const open = vi.spyOn(snackBar, 'open');
    const loaded = store.load(GAME_CODE);
    api.last('getGame').resolve(view);
    await loaded;
    const fixture = TestBed.createComponent(GamePlayPhase);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;

    const settle = async () => {
      await flushPromises();
      await fixture.whenStable();
    };
    const tap = async (label: string) => {
      host.querySelector<HTMLButtonElement>(`button[aria-label^="${label}"]`)?.click();
      await settle();
    };
    /** The open confirm snackbar's label, or `null` when none is open. */
    const confirmLabel = () =>
      document
        .querySelector('battle-shot-confirm-snackbar [matSnackBarLabel]')
        ?.textContent?.trim() ?? null;
    const action = (name: 'Cancel' | 'Shoot') =>
      Array.from(
        document.querySelectorAll<HTMLButtonElement>('battle-shot-confirm-snackbar button'),
      ).find((b) => b.textContent?.trim() === name) as HTMLButtonElement;
    return {
      fixture,
      host,
      store,
      api,
      openFromComponent,
      open,
      settle,
      tap,
      confirmLabel,
      action,
    };
  }

  it('opens a snackbar naming the cell with Cancel and Shoot and no auto-dismiss', async () => {
    const { tap, confirmLabel, action, openFromComponent } = await render(started(true));
    await tap('E7');
    expect(confirmLabel()).toBe('Fire at E7?');
    expect(action('Cancel')).toBeDefined();
    expect(action('Shoot')).toBeDefined();
    expect(openFromComponent).toHaveBeenCalledTimes(1);
    expect(openFromComponent.mock.calls[0][0]).toBe(ShotConfirmSnackbar);
    expect(openFromComponent.mock.calls[0][1]?.duration).toBeUndefined();
  });

  it('sends exactly one request on Shoot, then closes and draws the result', async () => {
    const { host, api, tap, settle, confirmLabel, action } = await render(started(true));
    await tap('E7');
    action('Shoot').click();
    await settle();
    expect(action('Shoot').disabled).toBe(true);
    action('Shoot').click();
    await settle();
    expect(api.of('fireShot').map((call) => call.args)).toEqual([[GAME_CODE, 4, 6]]);

    api.last('fireShot').resolve({ column: 4, row: 6, hit: true, state: 'started' });
    await settle();
    expect(confirmLabel()).toBeNull();
    expect(host.querySelector('[aria-label="E7, hit"] .shot-marker.is-hit')).not.toBeNull();
    expect(host.querySelectorAll('button[aria-disabled="true"]').length).toBe(144);
  });

  it('sends nothing on Cancel, removes the mark and keeps the turn', async () => {
    const { host, store, api, tap, settle, confirmLabel, action } = await render(started(true));
    await tap('E7');
    action('Cancel').click();
    await settle();
    expect(api.of('fireShot')).toEqual([]);
    expect(confirmLabel()).toBeNull();
    expect(host.querySelector('.is-target')).toBeNull();
    expect(store.myTurn()).toBe(true);
  });

  it('moves the target and renames the open snackbar', async () => {
    const { host, tap, confirmLabel, openFromComponent } = await render(started(true));
    await tap('E7');
    await tap('F2');
    expect(confirmLabel()).toBe('Fire at F2?');
    expect(openFromComponent).toHaveBeenCalledTimes(1);
    expect(
      Array.from(host.querySelectorAll('.is-target')).map((c) => c.getAttribute('aria-label')),
    ).toEqual(['F2, target']);
  });

  it('opens no snackbar without the turn', async () => {
    const { store, tap, confirmLabel, openFromComponent, fixture } = await render(started(false));
    store.selectTarget({ column: 4, row: 6 });
    await fixture.whenStable();
    await tap('E7');
    expect(confirmLabel()).toBeNull();
    expect(openFromComponent).not.toHaveBeenCalled();
  });

  it('opens no snackbar on a cell already fired at', async () => {
    const { tap, confirmLabel, openFromComponent } = await render(
      started(true, [{ column: 4, row: 6, hit: false }]),
    );
    await tap('E7');
    expect(confirmLabel()).toBeNull();
    expect(openFromComponent).not.toHaveBeenCalled();
  });

  it('closes the snackbar when the turn moves away', async () => {
    const { store, fixture, tap, confirmLabel } = await render(started(true));
    await tap('E7');
    store.applyTurnChanged(OPPONENT_ID);
    await fixture.whenStable();
    expect(confirmLabel()).toBeNull();
  });

  it('says the shot was not fired on a failure and lets the player select again', async () => {
    const { host, api, tap, settle, confirmLabel, action, open } = await render(started(true));
    await tap('E7');
    action('Shoot').click();
    await settle();
    api.last('fireShot').reject(new FireShotFailure('failed'));
    await settle();
    expect(confirmLabel()).toBeNull();
    expect(open).toHaveBeenCalledWith('The shot was not fired. Please try again.', 'Dismiss', {
      duration: SHOT_ERROR_DURATION,
    });
    expect(host.querySelector('[aria-label^="E7"] .shot-marker')).toBeNull();

    await tap('F2');
    expect(confirmLabel()).toBe('Fire at F2?');
  });

  it('explains a 409 refusal and reloads the game', async () => {
    const { api, tap, settle, action, open } = await render(started(true));
    await tap('E7');
    action('Shoot').click();
    await settle();
    api.last('fireShot').reject(new FireShotFailure('not-your-turn'));
    await settle();
    expect(open).toHaveBeenCalledWith('It is not your turn.', 'Dismiss', {
      duration: SHOT_ERROR_DURATION,
    });
    expect(api.of('getGame').length).toBe(2);
  });
});
