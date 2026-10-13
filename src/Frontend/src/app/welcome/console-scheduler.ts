import { computed, inject, Injectable, InjectionToken, signal } from '@angular/core';
import { CONSOLE_MESSAGES } from './console-messages';
import { formatConsoleTimestamp } from './console-timestamp';

// ---- Collaborators behind injection tokens, so the scheduler is testable without timers or DOM.

/** A random source in [0, 1). Defaults to `Math.random`. */
export const CONSOLE_RANDOM = new InjectionToken<() => number>('CONSOLE_RANDOM', {
  providedIn: 'root',
  factory: () => () => Math.random(),
});

/** The clock the timestamps are read from. Defaults to the current local time. */
export const CONSOLE_CLOCK = new InjectionToken<() => Date>('CONSOLE_CLOCK', {
  providedIn: 'root',
  factory: () => () => new Date(),
});

/** Whether the visitor asked for reduced motion. jsdom has no `matchMedia`, hence the guard. */
export const REDUCED_MOTION = new InjectionToken<boolean>('REDUCED_MOTION', {
  providedIn: 'root',
  factory: () =>
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches,
});

/** The message pool the scheduler draws from. Defaults to the static marine-themed pool. */
export const CONSOLE_MESSAGE_POOL = new InjectionToken<readonly string[]>('CONSOLE_MESSAGE_POOL', {
  providedIn: 'root',
  factory: () => CONSOLE_MESSAGES,
});

// ---- Timing contract (see the welcome-page spec).

/** The first line appears within this many milliseconds of the console starting. */
export const FIRST_DELAY_MAX_MS = 5000;
/** Every following line appears between these bounds, inclusive, after the previous one. */
export const NEXT_DELAY_MIN_MS = 5000;
export const NEXT_DELAY_MAX_MS = 15000;
/** One character is revealed per step while a line is being typed. */
export const TYPING_STEP_MS = 30;
/** The console keeps at most this many lines; older ones scroll out. */
export const CONSOLE_RING_SIZE = 12;

// ---- Pure rules, unit-tested without Angular.

/** Delay before the first line: 0 to 5000 ms inclusive. */
export function firstDelayMs(random: () => number): number {
  return Math.floor(clampUnit(random()) * (FIRST_DELAY_MAX_MS + 1));
}

/** Delay before every next line: 5000 to 15000 ms inclusive. */
export function nextDelayMs(random: () => number): number {
  const span = NEXT_DELAY_MAX_MS - NEXT_DELAY_MIN_MS + 1;
  return NEXT_DELAY_MIN_MS + Math.floor(clampUnit(random()) * span);
}

/** Picks a pool index at random, stepping past the previous one so no line repeats in a row. */
export function pickMessageIndex(
  random: () => number,
  poolSize: number,
  previous: number | null,
): number {
  const index = Math.floor(clampUnit(random()) * poolSize);
  return poolSize > 1 && index === previous ? (index + 1) % poolSize : index;
}

/** Keeps a random draw inside [0, 1) even when a stub hands back 1 or more. */
function clampUnit(value: number): number {
  return Math.min(Math.max(value, 0), 1 - Number.EPSILON);
}

// ---- State.

export interface ConsoleLine {
  readonly id: number;
  /** The `[ hh:mm:ss AM ] - ` prefix, shown whole as soon as the line appears. */
  readonly timestamp: string;
  readonly text: string;
  /** How many characters of `text` are visible; equals `text.length` once typing is done. */
  readonly typedLength: number;
}

export interface ConsoleState {
  readonly lines: readonly ConsoleLine[];
  readonly lastIndex: number | null;
  readonly nextId: number;
}

const initialState: ConsoleState = { lines: [], lastIndex: null, nextId: 1 };

/** Appends a line and drops the oldest ones beyond the ring size. */
export function appendLine(
  state: ConsoleState,
  line: Omit<ConsoleLine, 'id'>,
  poolIndex: number,
): ConsoleState {
  const lines = [...state.lines, { ...line, id: state.nextId }].slice(-CONSOLE_RING_SIZE);
  return { lines, lastIndex: poolIndex, nextId: state.nextId + 1 };
}

/** Reveals one more character of the given line. Returns the same state when nothing changes. */
export function advanceTyping(state: ConsoleState, id: number): ConsoleState {
  const index = state.lines.findIndex((line) => line.id === id);
  if (index < 0) return state;
  const line = state.lines[index];
  if (line.typedLength >= line.text.length) return state;
  const typed = { ...line, typedLength: line.typedLength + 1 };
  const lines = state.lines.map((entry, i) => (i === index ? typed : entry));
  return { ...state, lines };
}

/** Whether the given line has been typed out completely (or is gone from the ring). */
export function isTypingDone(state: ConsoleState, id: number): boolean {
  const line = state.lines.find((entry) => entry.id === id);
  return line === undefined || line.typedLength >= line.text.length;
}

/**
 * Drives the bridge console: draws a message at a random interval, appends it with a timestamp
 * and types it out one character at a time (or whole under reduced motion). Provided by the
 * console component so each console owns one scheduler; `stop()` cancels every pending timer.
 */
@Injectable()
export class ConsoleScheduler {
  readonly #random = inject(CONSOLE_RANDOM);
  readonly #clock = inject(CONSOLE_CLOCK);
  readonly #reducedMotion = inject(REDUCED_MOTION);
  readonly #pool = inject(CONSOLE_MESSAGE_POOL);

  readonly #state = signal<ConsoleState>(initialState);

  /** The visible lines, oldest first. */
  readonly lines = computed(() => this.#state().lines);

  #running = false;
  #messageTimer: ReturnType<typeof setTimeout> | null = null;
  #typingTimer: ReturnType<typeof setInterval> | null = null;

  /** Schedules the first line. Calling it while running does nothing. */
  start(): void {
    if (this.#running || this.#pool.length === 0) return;
    this.#running = true;
    this.#scheduleNext(firstDelayMs(this.#random));
  }

  /** Cancels every pending timer. The lines already shown stay on screen. */
  stop(): void {
    this.#running = false;
    if (this.#messageTimer !== null) {
      clearTimeout(this.#messageTimer);
      this.#messageTimer = null;
    }
    this.#stopTyping();
  }

  #scheduleNext(delayMs: number): void {
    this.#messageTimer = setTimeout(() => {
      this.#messageTimer = null;
      this.#showNext();
      if (this.#running) this.#scheduleNext(nextDelayMs(this.#random));
    }, delayMs);
  }

  #showNext(): void {
    const poolIndex = pickMessageIndex(this.#random, this.#pool.length, this.#state().lastIndex);
    const text = this.#pool[poolIndex];
    const line = {
      timestamp: formatConsoleTimestamp(this.#clock()),
      text,
      typedLength: this.#reducedMotion ? text.length : 0,
    };
    const id = this.#state().nextId;
    this.#state.update((state) => appendLine(state, line, poolIndex));
    if (!this.#reducedMotion) this.#startTyping(id);
  }

  #startTyping(id: number): void {
    this.#stopTyping();
    this.#typingTimer = setInterval(() => {
      const after = advanceTyping(this.#state(), id);
      this.#state.set(after);
      if (isTypingDone(after, id)) this.#stopTyping();
    }, TYPING_STEP_MS);
  }

  #stopTyping(): void {
    if (this.#typingTimer !== null) {
      clearInterval(this.#typingTimer);
      this.#typingTimer = null;
    }
  }
}
