import { TestBed } from '@angular/core/testing';
import { axe } from 'vitest-axe';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, provideFakeGamesApi } from '../../../testing/fake-games-api';
import { GAME_CODE, gameView, me, ship } from '../../../testing/game-views';
import { GameView } from '../game-view';
import { GamePageStore } from '../game-page/game-page.store';
import { By } from '@angular/platform-browser';
import { PlacementBoard } from './placement-board';
import { PlacementPhase } from './placement-phase';
import { ShipList } from './ship-list';

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

  function cell(host: HTMLElement, label: string): HTMLButtonElement {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('.placement-board__cell')).find(
      (b) => b.getAttribute('aria-label')?.startsWith(`${label},`),
    ) as HTMLButtonElement;
  }

  function listItem(host: HTMLElement, kind: string): HTMLButtonElement {
    return host.querySelector(`.ship-list__item[data-kind="${kind}"]`) as HTMLButtonElement;
  }

  it('places a tapped ship on the tapped cell', async () => {
    const { fixture, host, api, onBoard, listed } = await render();
    listItem(host, 'submarine').click();
    await fixture.whenStable();
    expect(listItem(host, 'submarine').getAttribute('aria-pressed')).toBe('true');

    cell(host, 'F6').click();
    await fixture.whenStable();
    expect(onBoard()).toEqual(['Submarine, F6 to H6, horizontal']);
    expect(listed()).not.toContain('Submarine');
    expect(api.last('placeShip').args).toEqual([
      GAME_CODE,
      'submarine',
      { column: 5, row: 5, orientation: 'horizontal' },
    ]);
  });

  it('moves a placed ship by keyboard, keeping its orientation', async () => {
    const { fixture, host, onBoard } = await render(
      gameView({ me: me({ ships: [ship('cruiser', 1, 1, 'vertical')] }) }),
    );
    const cruiser = host.querySelector('.placement-board__ship') as HTMLButtonElement;
    cruiser.focus();
    cruiser.click(); // Enter on a focused button
    await fixture.whenStable();
    expect(cruiser.getAttribute('aria-pressed')).toBe('true');

    cell(host, 'A1').focus();
    cell(host, 'A1').click();
    await fixture.whenStable();
    expect(onBoard()).toEqual(['Cruiser, A1 to A3, vertical']);
  });

  it('clears the active ship on Escape', async () => {
    const { fixture, host, store } = await render();
    listItem(host, 'carrier').click();
    await fixture.whenStable();
    listItem(host, 'carrier').dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
    );
    await fixture.whenStable();
    expect(store.activeKind()).toBeNull();
    expect(listItem(host, 'carrier').getAttribute('aria-pressed')).toBe('false');
  });

  it('follows a drag from the list over the board and places the ship on drop', async () => {
    const { fixture, host, api, onBoard } = await render();
    const board = fixture.debugElement.query(By.directive(PlacementBoard))
      .componentInstance as PlacementBoard;
    const list = fixture.debugElement.query(By.directive(ShipList)).componentInstance as ShipList;
    vi.spyOn(board, 'cellAt').mockReturnValue({ column: 2, row: 3 });

    list.shipDragStarted.emit({ kind: 'destroyer', orientation: 'horizontal', grabIndex: 0 });
    list.dragMoved.emit({ x: 10, y: 10 });
    await fixture.whenStable();
    expect(host.querySelector('.placement-board__preview.is-valid')).not.toBeNull();

    list.dragEnded.emit();
    await fixture.whenStable();
    expect(host.querySelector('.placement-board__preview')).toBeNull();
    expect(onBoard()).toEqual(['Destroyer, C4 to D4, horizontal']);
    expect(api.of('placeShip').length).toBe(1);
  });

  it('shows an invalid drag on the board and leaves the ship where it was', async () => {
    const { fixture, host, api, onBoard } = await render(
      gameView({ me: me({ ships: [ship('carrier', 0, 0), ship('battleship', 0, 5)] }) }),
    );
    const board = fixture.debugElement.query(By.directive(PlacementBoard))
      .componentInstance as PlacementBoard;
    vi.spyOn(board, 'cellAt').mockReturnValue({ column: 1, row: 0 });

    board.shipDragStarted.emit({ kind: 'battleship', orientation: 'horizontal', grabIndex: 0 });
    board.dragMoved.emit({ x: 10, y: 10 });
    await fixture.whenStable();
    expect(host.querySelector('.placement-board__preview.is-invalid')).not.toBeNull();

    board.dragEnded.emit();
    await fixture.whenStable();
    expect(onBoard()).toEqual([
      'Carrier, A1 to E1, horizontal',
      'Battleship, A6 to D6, horizontal',
    ]);
    expect(api.of('placeShip').length).toBe(0);
  });

  it('offers a rotate control for the active placed ship only and rotates it', async () => {
    const { fixture, host, onBoard } = await render(
      gameView({ me: me({ ships: [ship('cruiser', 1, 1)] }) }),
    );
    const rotate = () => host.querySelector<HTMLButtonElement>('.placement__rotate');
    expect(rotate()).toBeNull();

    (host.querySelector('.placement-board__ship') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(host.querySelector('.placement-board__ship.is-active')).not.toBeNull();
    expect(rotate()?.getAttribute('aria-label')).toBe('Rotate cruiser');

    rotate()?.click();
    await fixture.whenStable();
    expect(onBoard()).toEqual(['Cruiser, B2 to B4, vertical']);

    listItem(host, 'destroyer').click();
    await fixture.whenStable();
    expect(host.querySelector('.placement-board__ship.is-active')).toBeNull();
    expect(rotate()).toBeNull();
    expect(host.querySelector('.placement__selection')?.textContent).toContain(
      'Destroyer selected',
    );
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
