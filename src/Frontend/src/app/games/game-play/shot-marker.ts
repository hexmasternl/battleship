import { Component, input } from '@angular/core';

/**
 * One shot on a board: a hit is a red disc with a cross, a miss a small grey dot, so the two differ
 * by shape as well as colour. Decorative: the cell it sits in names the shot to assistive
 * technology.
 *
 * A fresh marker (a shot drawn after the board was loaded) scales and fades into place; one that
 * came with the load appears at rest, and under reduced motion every marker does.
 */
@Component({
  selector: 'battle-shot-marker',
  template: '',
  styleUrl: './shot-marker.scss',
  host: {
    class: 'shot-marker',
    'aria-hidden': 'true',
    '[class.is-hit]': 'hit()',
    '[class.is-miss]': '!hit()',
    '[class.is-fresh]': 'fresh()',
  },
})
export class ShotMarker {
  readonly hit = input.required<boolean>();
  /** Animate the marker into place. */
  readonly fresh = input(false);
}
