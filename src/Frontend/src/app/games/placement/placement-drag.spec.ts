import { grabIndexAt } from './placement-drag';

describe('grabIndexAt', () => {
  // A cruiser drawn 60px long: 20px per cell.
  const horizontal = { left: 100, top: 50, width: 60, height: 20 };
  const vertical = { left: 100, top: 50, width: 20, height: 60 };

  it('finds the grabbed cell along a horizontal ship', () => {
    expect(grabIndexAt({ x: 105, y: 55 }, horizontal, 3, 'horizontal')).toBe(0);
    expect(grabIndexAt({ x: 125, y: 55 }, horizontal, 3, 'horizontal')).toBe(1);
    expect(grabIndexAt({ x: 159, y: 55 }, horizontal, 3, 'horizontal')).toBe(2);
  });

  it('finds the grabbed cell along a vertical ship', () => {
    expect(grabIndexAt({ x: 105, y: 95 }, vertical, 3, 'vertical')).toBe(2);
  });

  it('clamps a grab outside the hull to its ends', () => {
    expect(grabIndexAt({ x: 40, y: 55 }, horizontal, 3, 'horizontal')).toBe(0);
    expect(grabIndexAt({ x: 400, y: 55 }, horizontal, 3, 'horizontal')).toBe(2);
    expect(
      grabIndexAt({ x: 1, y: 1 }, { left: 0, top: 0, width: 0, height: 0 }, 3, 'horizontal'),
    ).toBe(0);
  });
});
