import { TestBed } from '@angular/core/testing';
import { axe } from 'vitest-axe';
import { ship } from '../../../testing/game-views';
import { ShipView } from '../game-view';
import { PlacementBoard, cellFromPoint, neighbour } from './placement-board';
import { Cell } from './placement-rules';

describe('PlacementBoard', () => {
  async function render(ships: readonly ShipView[] = [], readOnly = false) {
    const fixture = TestBed.createComponent(PlacementBoard);
    fixture.componentRef.setInput('ships', ships);
    fixture.componentRef.setInput('readOnly', readOnly);
    const activated: Cell[] = [];
    const shipsActivated: string[] = [];
    let cleared = 0;
    fixture.componentInstance.cellActivated.subscribe((cell) => activated.push(cell));
    fixture.componentInstance.shipActivated.subscribe((kind) => shipsActivated.push(kind));
    fixture.componentInstance.cleared.subscribe(() => cleared++);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const cell = (label: string) =>
      Array.from(host.querySelectorAll<HTMLButtonElement>('.placement-board__cell')).find((b) =>
        b.getAttribute('aria-label')?.startsWith(`${label},`),
      ) as HTMLButtonElement;
    return {
      fixture,
      host,
      cell,
      activated,
      shipsActivated,
      cleared: () => cleared,
    };
  }

  it('shows an empty 12 x 12 grid labelled A to L and 1 to 12', async () => {
    const { host } = await render();
    const headers = Array.from(host.querySelectorAll('[role="columnheader"]')).map((h) =>
      h.textContent?.trim(),
    );
    expect(headers).toEqual(['Row', ...'ABCDEFGHIJKL'.split('')]);
    const rows = Array.from(host.querySelectorAll('[role="rowheader"]')).map((h) =>
      h.textContent?.trim(),
    );
    expect(rows).toEqual(Array.from({ length: 12 }, (_, i) => String(i + 1)));
    expect(host.querySelectorAll('[role="gridcell"]').length).toBe(144);
    expect(host.querySelectorAll('.placement-board__ship').length).toBe(0);
  });

  it('names each cell by its coordinate and what lies on it', async () => {
    const { cell } = await render([ship('cruiser', 2, 3)]);
    expect(cell('A1').getAttribute('aria-label')).toBe('A1, empty');
    expect(cell('C4').getAttribute('aria-label')).toBe('C4, cruiser');
    expect(cell('E4').getAttribute('aria-label')).toBe('E4, cruiser');
    expect(cell('F4').getAttribute('aria-label')).toBe('F4, empty');
  });

  it('draws placed ships over the cells they cover', async () => {
    const { host } = await render([ship('cruiser', 2, 3), ship('destroyer', 0, 0, 'vertical')]);
    const ships = Array.from(host.querySelectorAll<HTMLElement>('.placement-board__ship'));
    expect(ships.map((s) => s.getAttribute('aria-label'))).toEqual([
      'Cruiser, C4 to E4, horizontal',
      'Destroyer, A1 to A2, vertical',
    ]);
    expect(ships[0].style.gridColumn).toBe('4 / span 3');
    expect(ships[0].style.gridRow).toBe('5');
    expect(ships[1].classList).toContain('bs-ship--v');
    expect(ships[1].style.gridRow).toBe('2 / span 2');
  });

  it('keeps one cell in the tab order and moves it with the arrow keys', async () => {
    const { fixture, cell } = await render();
    expect(cell('A1').tabIndex).toBe(0);
    expect(cell('B1').tabIndex).toBe(-1);

    cell('A1').dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    await fixture.whenStable();
    expect(document.activeElement).toBe(cell('B1'));
    expect(cell('B1').tabIndex).toBe(0);
    expect(cell('A1').tabIndex).toBe(-1);
  });

  it('reports activated cells and ships, and Escape', async () => {
    const { host, cell, activated, shipsActivated, cleared } = await render([
      ship('cruiser', 2, 3),
    ]);
    cell('F6').click();
    (host.querySelector('.placement-board__ship') as HTMLButtonElement).click();
    host
      .querySelector('.bs-board')
      ?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(activated).toEqual([{ column: 5, row: 5 }]);
    expect(shipsActivated).toEqual(['cruiser']);
    expect(cleared()).toBe(1);
  });

  it('reports nothing while read only', async () => {
    const { host, cell, activated, shipsActivated } = await render([ship('cruiser', 2, 3)], true);
    cell('F6').click();
    (host.querySelector('.placement-board__ship') as HTMLButtonElement).click();
    expect(activated).toEqual([]);
    expect(shipsActivated).toEqual([]);
    expect(cell('F6').getAttribute('aria-disabled')).toBe('true');
  });

  it('has no accessibility violations', async () => {
    const { host } = await render([ship('cruiser', 2, 3)]);
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 20000);
});

describe('neighbour', () => {
  it('moves within the board and stops at its edges', () => {
    expect(neighbour({ column: 0, row: 0 }, 'ArrowLeft')).toEqual({ column: 0, row: 0 });
    expect(neighbour({ column: 0, row: 0 }, 'ArrowDown')).toEqual({ column: 0, row: 1 });
    expect(neighbour({ column: 4, row: 4 }, 'End')).toEqual({ column: 11, row: 4 });
    expect(neighbour({ column: 4, row: 4 }, 'Home', true)).toEqual({ column: 0, row: 0 });
    expect(neighbour({ column: 4, row: 4 }, 'a')).toBeNull();
  });
});

describe('cellFromPoint', () => {
  // Cells 20px wide with a 1px gap: A1 at (100, 50), L12 at (331, 281).
  const first = { left: 100, top: 50 };
  const last = { left: 331, top: 281, right: 351, bottom: 301 };

  it('finds the cell under a point', () => {
    expect(cellFromPoint({ x: 105, y: 55 }, first, last)).toEqual({ column: 0, row: 0 });
    expect(cellFromPoint({ x: 100 + 21 * 2 + 5, y: 50 + 21 * 3 + 5 }, first, last)).toEqual({
      column: 2,
      row: 3,
    });
    expect(cellFromPoint({ x: 350, y: 300 }, first, last)).toEqual({ column: 11, row: 11 });
  });

  it('answers null off the board', () => {
    expect(cellFromPoint({ x: 99, y: 55 }, first, last)).toBeNull();
    expect(cellFromPoint({ x: 105, y: 302 }, first, last)).toBeNull();
  });
});
