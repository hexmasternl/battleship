import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FLEET, InfoSection } from './info-section';

describe('InfoSection', () => {
  let fixture: ComponentFixture<InfoSection>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [InfoSection] }).compileComponents();
    fixture = TestBed.createComponent(InfoSection);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  it('shows at least four cards, each with a heading and copy', () => {
    const cards = host.querySelectorAll('.bs-card');
    expect(cards.length).toBeGreaterThanOrEqual(4);
    for (const card of Array.from(cards)) {
      expect(card.querySelector('h3')).not.toBeNull();
      expect(card.querySelector('p')?.textContent?.trim().length).toBeGreaterThan(0);
    }
  });

  it('covers the four topics', () => {
    const headings = Array.from(host.querySelectorAll('.bs-card h3')).map((h) =>
      h.textContent?.trim(),
    );
    expect(headings).toEqual(
      expect.arrayContaining(['How a game works', 'The fleet', 'The grid', 'Turns and hits']),
    );
  });

  it('names the five ships with their lengths', () => {
    const ships = Array.from(host.querySelectorAll('.fleet__ship'));
    expect(ships.length).toBe(5);
    const names = ships.map((ship) => ship.querySelector('.fleet__name')?.textContent?.trim());
    expect(names).toEqual(['Carrier', 'Battleship', 'Destroyer', 'Submarine', 'Patrol Boat']);
    const lengths = ships.map((ship) => ship.querySelector('.fleet__length')?.textContent?.trim());
    expect(lengths).toEqual(['5 cells', '4 cells', '3 cells', '3 cells', '2 cells']);
  });

  it('draws each ship bar in proportion to its length', () => {
    const bars = Array.from(host.querySelectorAll<HTMLElement>('.fleet__bar'));
    expect(bars.map((bar) => bar.style.getPropertyValue('--ship-length'))).toEqual(
      FLEET.map((ship) => String(ship.length)),
    );
  });

  it('describes the 12 by 12 grid with its coordinates', () => {
    const text = host.textContent ?? '';
    expect(text).toContain('12 by 12');
    expect(text).toContain('A to L');
    expect(text).toContain('1 to 12');
  });

  it('shows the miss, hit and sunk markers', () => {
    expect(host.querySelector('.legend__cell--miss')).not.toBeNull();
    expect(host.querySelector('.legend__cell--hit')).not.toBeNull();
    expect(host.querySelector('.legend__cell--sunk')).not.toBeNull();
  });
});
