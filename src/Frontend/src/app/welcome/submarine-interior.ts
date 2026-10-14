import { Component, inject } from '@angular/core';
import { REDUCED_MOTION } from './console-scheduler';

/**
 * The hero's backdrop: an inline, animated vector drawing of a submarine control room, 1920 by
 * 1080 user units, cropped like a cover image to the hero. Its colours are design system tokens
 * (each with the documented value as fallback, so the SVG file also renders on its own), and all
 * of its motion is CSS: blinking lamps with a glow while lit, swinging gauge needles, a radar
 * sweep, sonar pings, steam from two relief vents, a fan and level meters. It is decoration and
 * hidden from assistive technology; the reduced-motion host class and the media query freeze it.
 */
@Component({
  selector: 'battle-submarine-interior',
  host: {
    class: 'submarine',
    'aria-hidden': 'true',
    '[class.submarine--reduced-motion]': 'reducedMotion',
  },
  templateUrl: './submarine-interior.svg',
  styleUrl: './submarine-interior.scss',
})
export class SubmarineInterior {
  protected readonly reducedMotion = inject(REDUCED_MOTION);
}
