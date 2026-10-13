import { Component, input } from '@angular/core';

/**
 * One shot on a board: a hit is a red disc with a cross, a miss a small grey dot, so the two differ
 * by shape as well as colour. Decorative: the cell it sits in names the shot to assistive
 * technology.
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
  },
})
export class ShotMarker {
  readonly hit = input.required<boolean>();
}
