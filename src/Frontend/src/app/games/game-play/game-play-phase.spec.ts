import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatTabGroupHarness } from '@angular/material/tabs/testing';
import { axe } from 'vitest-axe';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, provideFakeGamesApi } from '../../../testing/fake-games-api';
import {
  FULL_FLEET,
  GAME_CODE,
  ME_ID,
  OPPONENT_ID,
  gameView,
  me,
  opponent,
} from '../../../testing/game-views';
import { GamePageStore } from '../game-page/game-page.store';
import { GameView } from '../game-view';
import { GamePlayPhase } from './game-play-phase';

/** A started game: the caller's fleet in place, both ready, the turn as given. */
function started(
  myTurn: boolean,
  shots: { mine?: GameView['me']['shots']; theirs?: GameView['me']['shots'] } = {},
): GameView {
  return gameView({
    state: 'started',
    me: me({ ready: true, hasTurn: myTurn, ships: FULL_FLEET, shots: shots.mine ?? [] }),
    opponent: opponent({ ready: true, hasTurn: !myTurn, shots: shots.theirs ?? [] }),
  });
}

describe('GamePlayPhase', () => {
  async function render(view: GameView) {
    const api = new FakeGamesApi();
    const events = new FakeEventsService();
    TestBed.configureTestingModule({
      imports: [GamePlayPhase],
      providers: [
        provideFakeGamesApi(api),
        provideFakeEvents(events),
        // jsdom fires no transition events, so the tab bodies swap only without animations.
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
    const selectedTab = async () => (await tabs.getSelectedTab()).getLabel();
    const turnLine = () => host.querySelector('[aria-live="polite"]')?.textContent?.trim();
    const label = (selector: string) =>
      Array.from(host.querySelectorAll(selector)).map((el) => el.getAttribute('aria-label'));
    return { fixture, host, store, api, events, tabs, selectedTab, turnLine, label };
  }

  describe('tabs and turn', () => {
    it('opens on You and says it is the caller turn when they have it', async () => {
      const { selectedTab, turnLine } = await render(started(true));
      expect(await selectedTab()).toBe('You');
      expect(turnLine()).toBe('Your turn');
    });

    it('opens on Opponent and says it is the opponent turn otherwise', async () => {
      const { selectedTab, turnLine } = await render(started(false));
      expect(await selectedTab()).toBe('Opponent');
      expect(turnLine()).toBe("Opponent's turn");
    });

    it('labels the two tabs You and Opponent', async () => {
      const { tabs } = await render(started(true));
      const labels = await Promise.all((await tabs.getTabs()).map((tab) => tab.getLabel()));
      expect(labels).toEqual(['You', 'Opponent']);
    });

    it('follows a turn change to the opponent and back', async () => {
      const { fixture, store, selectedTab, turnLine } = await render(started(true));
      store.applyTurnChanged(OPPONENT_ID);
      await fixture.whenStable();
      expect(await selectedTab()).toBe('Opponent');
      expect(turnLine()).toBe("Opponent's turn");
      expect(fixture.nativeElement.querySelector('battle-fleet-grid')).not.toBeNull();

      store.applyTurnChanged(ME_ID);
      await fixture.whenStable();
      expect(await selectedTab()).toBe('You');
      expect(turnLine()).toBe('Your turn');
    });

    it('keeps a manual tab choice until the turn moves', async () => {
      const { fixture, store, tabs, selectedTab } = await render(started(false));
      await tabs.selectTab({ label: 'You' });
      expect(await selectedTab()).toBe('You');

      // A turn event that does not move the turn leaves the manual choice alone.
      store.applyTurnChanged(OPPONENT_ID);
      await fixture.whenStable();
      expect(await selectedTab()).toBe('You');

      // The turn comes back while "You" is selected by hand: it stays selected and cells work.
      store.applyTurnChanged(ME_ID);
      await fixture.whenStable();
      expect(await selectedTab()).toBe('You');
      expect(store.canShoot()).toBe(true);

      await tabs.selectTab({ label: 'Opponent' });
      store.applyTurnChanged(OPPONENT_ID);
      await fixture.whenStable();
      expect(await selectedTab()).toBe('Opponent');
      store.applyTurnChanged(ME_ID);
      await fixture.whenStable();
      expect(await selectedTab()).toBe('You');
    });
  });

  describe('You tab', () => {
    it('shows only the caller shots, a hit and a miss, and no ships', async () => {
      const { host, label } = await render(
        started(true, {
          mine: [
            { column: 2, row: 4, hit: true },
            { column: 3, row: 8, hit: false },
          ],
          theirs: [{ column: 0, row: 0, hit: true }],
        }),
      );
      expect(host.querySelectorAll('battle-target-grid button').length).toBe(144);
      expect(host.querySelectorAll('battle-shot-marker').length).toBe(2);
      expect(host.querySelector('[aria-label="C5, hit"] battle-shot-marker.is-hit')).not.toBeNull();
      expect(
        host.querySelector('[aria-label="D9, miss"] battle-shot-marker.is-miss'),
      ).not.toBeNull();
      expect(label('battle-target-grid button').filter((l) => l?.includes(','))).toEqual([
        'C5, hit',
        'D9, miss',
      ]);
      expect(host.querySelector('.bs-ship')).toBeNull();
    });

    it('selects a target from a cell and marks it', async () => {
      const { fixture, host, store } = await render(started(true));
      (host.querySelector('[aria-label="E7"]') as HTMLButtonElement).click();
      await fixture.whenStable();
      expect(store.target()).toEqual({ column: 4, row: 6 });
      const target = host.querySelector('[aria-label="E7, target"]');
      expect(target?.classList).toContain('is-target');
    });
  });

  describe('Opponent tab', () => {
    it('shows all five ships and the opponent hit on the carrier', async () => {
      const { host } = await render(started(false, { theirs: [{ column: 1, row: 0, hit: true }] }));
      expect(host.querySelectorAll('battle-fleet-grid .bs-ship').length).toBe(5);
      expect(
        host.querySelector(
          '[role="cell"][aria-label="B1, carrier, hit"] battle-shot-marker.is-hit',
        ),
      ).not.toBeNull();
      expect(host.querySelectorAll('battle-shot-marker').length).toBe(1);
    });

    it('never shows the opponent ships, even with their shots on the board', async () => {
      const { fixture, host, tabs } = await render(
        started(true, { mine: [{ column: 5, row: 5, hit: true }] }),
      );
      expect(host.querySelector('battle-target-grid .bs-ship')).toBeNull();
      await tabs.selectTab({ label: 'Opponent' });
      await fixture.whenStable();
      // Only the caller's own five ships exist anywhere in the view.
      expect(host.querySelectorAll('.bs-ship').length).toBe(5);
    });
  });

  it('has no accessibility violations on either tab', async () => {
    const { host, tabs } = await render(
      started(true, {
        mine: [{ column: 2, row: 4, hit: true }],
        theirs: [{ column: 0, row: 0, hit: false }],
      }),
    );
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
      await tabs.selectTab({ label: 'Opponent' });
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 30000);
});
