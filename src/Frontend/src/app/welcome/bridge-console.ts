import { Component, computed, DestroyRef, inject } from '@angular/core';
import { ConsoleScheduler, REDUCED_MOTION } from './console-scheduler';

/** One rendered console row: the visible slice of the text plus whether it is still typing. */
interface ConsoleRow {
  readonly id: number;
  readonly timestamp: string;
  readonly text: string;
  readonly fullText: string;
  readonly typing: boolean;
}

/**
 * The bridge console in the hero's right panel: a terminal that types random marine-themed
 * messages with a timestamp, newest at the bottom. Each console owns one scheduler, started on
 * construction and stopped when the component is destroyed so no timer survives a route change.
 */
@Component({
  selector: 'battle-bridge-console',
  providers: [ConsoleScheduler],
  host: { class: 'bs-panel console', '[class.console--reduced-motion]': 'reducedMotion' },
  templateUrl: './bridge-console.html',
  styleUrl: './bridge-console.scss',
})
export class BridgeConsole {
  readonly #scheduler = inject(ConsoleScheduler);

  protected readonly reducedMotion = inject(REDUCED_MOTION);

  protected readonly rows = computed<readonly ConsoleRow[]>(() =>
    this.#scheduler.lines().map((line) => ({
      id: line.id,
      timestamp: line.timestamp,
      text: line.text.slice(0, line.typedLength),
      fullText: line.text,
      typing: line.typedLength < line.text.length,
    })),
  );

  constructor() {
    this.#scheduler.start();
    inject(DestroyRef).onDestroy(() => this.#scheduler.stop());
  }
}
