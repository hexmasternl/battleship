import { Orientation, ShipKind } from '../game-view';

/** A point in viewport coordinates. */
export interface Point {
  readonly x: number;
  readonly y: number;
}

/** What a placement drag starts with: the ship, how it lies and which of its cells was grabbed. */
export interface ShipDragStart {
  readonly kind: ShipKind;
  readonly orientation: Orientation;
  readonly grabIndex: number;
}

/**
 * Which cell of a ship drawn in `rect` lies under `point`, counted from the bow along the ship
 * and clamped to the ship, so a grab outside the hull counts as its first or last cell.
 */
export function grabIndexAt(
  point: Point,
  rect: Pick<DOMRect, 'left' | 'top' | 'width' | 'height'>,
  length: number,
  orientation: Orientation,
): number {
  const extent = orientation === 'horizontal' ? rect.width : rect.height;
  const offset = orientation === 'horizontal' ? point.x - rect.left : point.y - rect.top;
  if (extent <= 0) return 0;
  return Math.min(Math.max(Math.floor((offset / extent) * length), 0), length - 1);
}
