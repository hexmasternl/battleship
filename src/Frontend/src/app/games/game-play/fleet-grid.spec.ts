import { TestBed } from '@angular/core/testing';
import { FULL_FLEET, ship } from '../../../testing/game-views';
import { ShipView, ShotView } from '../game-view';
import { FleetGrid } from './fleet-grid';

describe('FleetGrid', () => {
  async function render(ships: readonly ShipView[], shots: readonly ShotView[] = []) {
    TestBed.configureTestingModule({ imports: [FleetGrid] });
    const fixture = TestBed.createComponent(FleetGrid);
    fixture.componentRef.setInput('ships', ships);
    fixture.componentRef.setInput('shots', shots);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const label = (index: number) =>
      host.querySelectorAll('[role="cell"]')[index].getAttribute('aria-label');
    return { fixture, host, label };
  }

  it('is a read-only 12 x 12 table with no buttons', async () => {
    const { host } = await render(FULL_FLEET);
    expect(host.querySelectorAll('[role="cell"]').length).toBe(144);
    expect(host.querySelector('button')).toBeNull();
    expect(host.querySelector('[role="table"]')).not.toBeNull();
  });

  it('draws every ship of the caller', async () => {
    const { host } = await render([ship('carrier', 0, 0), ship('cruiser', 2, 3, 'vertical')]);
    const ships = Array.from(host.querySelectorAll<HTMLElement>('.bs-ship'));
    expect(ships.length).toBe(2);
    expect(ships[1].classList).toContain('bs-ship--v');
    expect(ships[0].style.gridColumn).toContain('span 5');
  });

  it('labels each cell with its coordinate, its ship and the opponent shot', async () => {
    const { label } = await render(
      [ship('carrier', 0, 0)],
      [
        { column: 1, row: 0, hit: true },
        { column: 3, row: 8, hit: false },
      ],
    );
    expect(label(0)).toBe('A1, carrier');
    expect(label(1)).toBe('B1, carrier, hit');
    expect(label(5)).toBe('F1');
    expect(label(8 * 12 + 3)).toBe('D9, miss');
  });

  it('draws a marker per opponent shot', async () => {
    const { host } = await render(FULL_FLEET, [
      { column: 0, row: 0, hit: true },
      { column: 11, row: 11, hit: false },
    ]);
    expect(host.querySelectorAll('.shot-marker.is-hit').length).toBe(1);
    expect(host.querySelectorAll('.shot-marker.is-miss').length).toBe(1);
  });
});
