import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatTabGroupHarness } from '@angular/material/tabs/testing';
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
import { GameView } from '../game-view';
import { GamePlayPhase } from './game-play-phase';
import { ShotMarker } from './shot-marker';

const MY_SHOT = { column: 2, row: 4, hit: true };
const THEIR_SHOT = { column: 0, row: 0, hit: false };

function started(myTurn: boolean): GameView {
  return gameView({
    state: 'started',
    me: me({ ready: true, hasTurn: myTurn, ships: FULL_FLEET, shots: [MY_SHOT] }),
    opponent: opponent({ ready: true, hasTurn: !myTurn, shots: [THEIR_SHOT] }),
  });
}

describe('Shot marker animation', () => {
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
    const loaded = store.load(GAME_CODE);
    api.last('getGame').resolve(view);
    await loaded;
    const fixture = TestBed.createComponent(GamePlayPhase);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const tabs = await TestbedHarnessEnvironment.loader(fixture).getHarness(MatTabGroupHarness);
    const marker = (label: string) =>
      host.querySelector<HTMLElement>(`[aria-label="${label}"] .shot-marker`);
    const settle = async () => {
      await flushPromises();
      await fixture.whenStable();
    };
    return { fixture, host, store, api, tabs, marker, settle };
  }

  it('does not animate markers present when the view first renders', async () => {
    const { marker, tabs } = await render(started(true));
    expect(marker('C5, hit')?.classList).not.toContain('is-fresh');
    await tabs.selectTab({ label: 'Opponent' });
    expect(marker('A1, carrier, miss')?.classList).not.toContain('is-fresh');
  });

  it('animates the marker of a shot recorded by the response', async () => {
    const { store, api, marker, settle } = await render(started(true));
    store.selectTarget({ column: 4, row: 6 });
    const done = store.fireAtTarget();
    api.last('fireShot').resolve({ column: 4, row: 6, hit: false });
    await done;
    await settle();
    expect(marker('E7, miss')?.classList).toContain('is-fresh');
    expect(marker('C5, hit')?.classList).not.toContain('is-fresh');
  });

  it('animates the marker of an opponent shot from an event', async () => {
    const { store, fixture, marker } = await render(started(false));
    store.applyShotFired({ playerId: OPPONENT_ID, column: 1, row: 0, hit: true });
    await fixture.whenStable();
    expect(marker('B1, carrier, hit')?.classList).toContain('is-fresh');
    expect(marker('A1, carrier, miss')?.classList).not.toContain('is-fresh');
  });

  it('does not animate markers after a reload, including ones it brings', async () => {
    const { store, api, fixture, marker, settle } = await render(started(false));
    store.applyShotFired({ playerId: OPPONENT_ID, column: 1, row: 0, hit: true });
    await fixture.whenStable();

    const reloaded = store.reload();
    api.last('getGame').resolve(
      gameView({
        state: 'started',
        me: me({ ready: true, hasTurn: false, ships: FULL_FLEET, shots: [MY_SHOT] }),
        opponent: opponent({
          ready: true,
          hasTurn: true,
          shots: [THEIR_SHOT, { column: 1, row: 0, hit: true }, { column: 5, row: 5, hit: false }],
        }),
      }),
    );
    await reloaded;
    await settle();
    expect(marker('B1, carrier, hit')?.classList).not.toContain('is-fresh');
    expect(marker('F6, miss')?.classList).not.toContain('is-fresh');
  });

  it('keeps the boards when tabs switch, so a marker does not land twice', async () => {
    const { store, fixture, tabs, marker } = await render(started(false));
    store.applyShotFired({ playerId: OPPONENT_ID, column: 1, row: 0, hit: true });
    await fixture.whenStable();
    const landed = marker('B1, carrier, hit');

    await tabs.selectTab({ label: 'You' });
    await tabs.selectTab({ label: 'Opponent' });
    expect(marker('B1, carrier, hit')).toBe(landed);
  });

  describe('styles', () => {
    /** The compiled styles of `ShotMarker`, as Angular adds them to the document. */
    async function markerStyles(): Promise<string> {
      TestBed.configureTestingModule({ imports: [ShotMarker] });
      const fixture = TestBed.createComponent(ShotMarker);
      fixture.componentRef.setInput('hit', true);
      fixture.componentRef.setInput('fresh', true);
      await fixture.whenStable();
      return Array.from(document.querySelectorAll('style'))
        .map((style) => style.textContent ?? '')
        .filter((css) => css.includes('shot-marker-enter'))
        .join('\n');
    }

    it('animates a fresh marker with the base duration and the standard easing', async () => {
      const css = (await markerStyles()).replace(/\s+/g, ' ');
      // Angular scopes the keyframes name to the component, so it may carry a prefix.
      expect(css).toMatch(
        /\.is-fresh[^{]*\{ ?animation: ?\S*shot-marker-enter var\(--bs-duration-base\) var\(--bs-ease-standard\)/,
      );
      expect(css).toMatch(/@keyframes \S*shot-marker-enter/);
    });

    it('turns the animation off under reduced motion', async () => {
      const css = (await markerStyles()).replace(/\s+/g, ' ');
      expect(css).toMatch(
        /@media \(prefers-reduced-motion: ?reduce\) ?\{[^}]*\.is-fresh[^{]*\{ ?animation: ?none/,
      );
    });
  });
});
