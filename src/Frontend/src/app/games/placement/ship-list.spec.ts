import { TestBed } from '@angular/core/testing';
import { axe } from 'vitest-axe';
import { FLEET, FleetShip } from './placement-rules';
import { ShipList } from './ship-list';

describe('ShipList', () => {
  async function render(ships: readonly FleetShip[] = FLEET) {
    const fixture = TestBed.createComponent(ShipList);
    fixture.componentRef.setInput('ships', ships);
    const activated: string[] = [];
    fixture.componentInstance.shipActivated.subscribe((kind) => activated.push(kind));
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const items = () => Array.from(host.querySelectorAll<HTMLButtonElement>('.ship-list__item'));
    return { fixture, host, items, activated };
  }

  it('lists every ship with its name and length, drawn at its length', async () => {
    const { items } = await render();
    expect(
      items().map((item) => [
        item.querySelector('.ship-list__name')?.textContent?.trim(),
        item.querySelector('.ship-list__length')?.textContent?.trim(),
        (item.querySelector('.ship-list__hull') as HTMLElement).style.getPropertyValue(
          '--ship-length',
        ),
      ]),
    ).toEqual([
      ['Carrier', '5 cells', '5'],
      ['Battleship', '4 cells', '4'],
      ['Cruiser', '3 cells', '3'],
      ['Submarine', '3 cells', '3'],
      ['Destroyer', '2 cells', '2'],
    ]);
  });

  it('says so when every ship is placed', async () => {
    const { host, items } = await render([]);
    expect(items()).toEqual([]);
    expect(host.textContent).toContain('All ships are on the board.');
  });

  it('reports a selected ship and marks the active one', async () => {
    const { fixture, items, activated } = await render();
    items()[2].click();
    expect(activated).toEqual(['cruiser']);

    fixture.componentRef.setInput('activeKind', 'cruiser');
    await fixture.whenStable();
    expect(items()[2].getAttribute('aria-pressed')).toBe('true');
    expect(items()[2].classList).toContain('is-active');
    expect(items()[0].getAttribute('aria-pressed')).toBe('false');
  });

  it('has no accessibility violations', async () => {
    const { host } = await render();
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 20000);
});
