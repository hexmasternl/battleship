import { TestBed } from '@angular/core/testing';
import { axe } from 'vitest-axe';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, provideFakeGamesApi } from '../../../testing/fake-games-api';
import { GAME_CODE, gameView, me, ship } from '../../../testing/game-views';
import { GameView } from '../game-view';
import { GamePageStore } from '../game-page/game-page.store';
import { PlacementPhase } from './placement-phase';

describe('PlacementPhase', () => {
  async function render(view: GameView = gameView()) {
    const api = new FakeGamesApi();
    TestBed.configureTestingModule({
      imports: [PlacementPhase],
      providers: [
        provideFakeGamesApi(api),
        provideFakeEvents(new FakeEventsService()),
        GamePageStore,
      ],
    });
    const store = TestBed.inject(GamePageStore);
    const loaded = store.load(GAME_CODE);
    api.last('getGame').resolve(view);
    await loaded;
    const fixture = TestBed.createComponent(PlacementPhase);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const listed = () =>
      Array.from(host.querySelectorAll('.ship-list__name')).map((n) => n.textContent?.trim());
    const onBoard = () =>
      Array.from(host.querySelectorAll('.placement-board__ship')).map((s) =>
        s.getAttribute('aria-label'),
      );
    return { fixture, host, store, api, listed, onBoard };
  }

  it('shows an empty board and the five ships to place', async () => {
    const { host, listed, onBoard } = await render();
    expect(host.querySelectorAll('[role="gridcell"]').length).toBe(144);
    expect(onBoard()).toEqual([]);
    expect(listed()).toEqual(['Carrier', 'Battleship', 'Cruiser', 'Submarine', 'Destroyer']);
  });

  it('shows placed ships on the board and only the others in the list', async () => {
    const { listed, onBoard } = await render(
      gameView({ me: me({ ships: [ship('carrier', 0, 0), ship('cruiser', 2, 3, 'vertical')] }) }),
    );
    expect(onBoard()).toEqual(['Carrier, A1 to E1, horizontal', 'Cruiser, C4 to C6, vertical']);
    expect(listed()).toEqual(['Battleship', 'Submarine', 'Destroyer']);
  });

  it('has no accessibility violations', async () => {
    const { host } = await render(gameView({ me: me({ ships: [ship('carrier', 0, 0)] }) }));
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 30000);
});
