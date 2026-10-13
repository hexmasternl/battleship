import { ship } from '../../../testing/game-views';
import {
  BOARD_SIZE,
  FLEET,
  cellLabel,
  cellsOf,
  checkPlacement,
  isOnBoard,
  lengthOf,
  refusalMessage,
  rotated,
  withPlacement,
} from './placement-rules';

describe('placement rules', () => {
  describe('fleet', () => {
    it('is the classic fleet with lengths 5, 4, 3, 3, 2', () => {
      expect(FLEET.map((f) => [f.kind, f.length])).toEqual([
        ['carrier', 5],
        ['battleship', 4],
        ['cruiser', 3],
        ['submarine', 3],
        ['destroyer', 2],
      ]);
      expect(lengthOf('submarine')).toBe(3);
    });
  });

  describe('cells', () => {
    it('runs to the right from the origin when horizontal', () => {
      expect(cellsOf(ship('cruiser', 1, 1))).toEqual([
        { column: 1, row: 1 },
        { column: 2, row: 1 },
        { column: 3, row: 1 },
      ]);
    });

    it('runs downwards from the origin when vertical', () => {
      expect(cellsOf(ship('destroyer', 4, 6, 'vertical'))).toEqual([
        { column: 4, row: 6 },
        { column: 4, row: 7 },
      ]);
    });
  });

  describe('board', () => {
    it('is 12 by 12 with zero-based coordinates', () => {
      expect(BOARD_SIZE).toBe(12);
      expect(isOnBoard({ column: 0, row: 0 })).toBe(true);
      expect(isOnBoard({ column: 11, row: 11 })).toBe(true);
      expect(isOnBoard({ column: 12, row: 0 })).toBe(false);
      expect(isOnBoard({ column: 0, row: 12 })).toBe(false);
      expect(isOnBoard({ column: -1, row: 0 })).toBe(false);
    });

    it('labels cells with a column letter and a one-based row', () => {
      expect(cellLabel({ column: 2, row: 3 })).toBe('C4');
      expect(cellLabel({ column: 11, row: 11 })).toBe('L12');
    });
  });

  describe('checkPlacement', () => {
    it('accepts a first placement on the board', () => {
      expect(checkPlacement(ship('carrier', 0, 0), [])).toEqual({ ok: true });
    });

    it('accepts a ship ending on the last column and row', () => {
      expect(checkPlacement(ship('carrier', 7, 11), [])).toEqual({ ok: true });
      expect(checkPlacement(ship('carrier', 11, 7, 'vertical'), [])).toEqual({ ok: true });
    });

    it('refuses a ship that leaves the board', () => {
      expect(checkPlacement(ship('carrier', 8, 0), [])).toEqual({
        ok: false,
        reason: 'out-of-board',
      });
      expect(checkPlacement(ship('carrier', 0, 9, 'vertical'), [])).toEqual({
        ok: false,
        reason: 'out-of-board',
      });
    });

    it('refuses a ship overlapping another ship and names it', () => {
      const fleet = [ship('carrier', 0, 0)];
      expect(checkPlacement(ship('battleship', 2, 0, 'vertical'), fleet)).toEqual({
        ok: false,
        reason: 'overlap',
        other: 'carrier',
      });
    });

    it('checks the board before overlap', () => {
      const fleet = [ship('carrier', 9, 0, 'vertical')];
      expect(checkPlacement(ship('battleship', 9, 0), fleet)).toEqual({
        ok: false,
        reason: 'out-of-board',
      });
    });

    it('allows touching ships', () => {
      const fleet = [ship('carrier', 0, 0)];
      expect(checkPlacement(ship('battleship', 0, 1), fleet)).toEqual({ ok: true });
      expect(checkPlacement(ship('destroyer', 5, 0), fleet)).toEqual({ ok: true });
    });

    it('lets a ship move onto the cells it covers now', () => {
      const fleet = [ship('carrier', 0, 0)];
      expect(checkPlacement(ship('carrier', 1, 0), fleet)).toEqual({ ok: true });
    });
  });

  describe('withPlacement', () => {
    it('adds a new ship in fleet order', () => {
      const fleet = withPlacement([ship('destroyer', 0, 5)], ship('carrier', 0, 0));
      expect(fleet.map((s) => s.kind)).toEqual(['carrier', 'destroyer']);
    });

    it('replaces the earlier placement of the same ship', () => {
      const fleet = withPlacement([ship('carrier', 0, 0)], ship('carrier', 3, 3, 'vertical'));
      expect(fleet).toEqual([ship('carrier', 3, 3, 'vertical')]);
    });
  });

  describe('rotation', () => {
    it('toggles the orientation and keeps the origin', () => {
      expect(rotated(ship('cruiser', 1, 1))).toEqual(ship('cruiser', 1, 1, 'vertical'));
      expect(rotated(ship('cruiser', 1, 1, 'vertical'))).toEqual(ship('cruiser', 1, 1));
    });

    it('explains a refused rotation', () => {
      expect(refusalMessage('carrier', { ok: false, reason: 'out-of-board' })).toBe(
        'The carrier would leave the board',
      );
      expect(refusalMessage('cruiser', { ok: false, reason: 'overlap', other: 'battleship' })).toBe(
        'The cruiser would overlap the battleship',
      );
      expect(refusalMessage('cruiser', { ok: true })).toBeNull();
    });
  });
});
