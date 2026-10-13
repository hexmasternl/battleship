import { TestBed } from '@angular/core/testing';
import {
  advanceTyping,
  appendLine,
  CONSOLE_CLOCK,
  CONSOLE_MESSAGE_POOL,
  CONSOLE_RANDOM,
  CONSOLE_RING_SIZE,
  ConsoleScheduler,
  ConsoleState,
  FIRST_DELAY_MAX_MS,
  firstDelayMs,
  NEXT_DELAY_MAX_MS,
  NEXT_DELAY_MIN_MS,
  nextDelayMs,
  pickMessageIndex,
  REDUCED_MOTION,
  TYPING_STEP_MS,
} from './console-scheduler';

const POOL = ['Alpha', 'Bravo', 'Charlie'];
const CLOCK = () => new Date(2026, 9, 13, 10, 18, 37);

interface SetupOptions {
  readonly random?: () => number;
  readonly reducedMotion?: boolean;
  readonly pool?: readonly string[];
}

function setup(options: SetupOptions = {}): ConsoleScheduler {
  TestBed.configureTestingModule({
    providers: [
      ConsoleScheduler,
      { provide: CONSOLE_RANDOM, useValue: options.random ?? (() => 0.5) },
      { provide: CONSOLE_CLOCK, useValue: CLOCK },
      { provide: REDUCED_MOTION, useValue: options.reducedMotion ?? false },
      { provide: CONSOLE_MESSAGE_POOL, useValue: options.pool ?? POOL },
    ],
  });
  return TestBed.inject(ConsoleScheduler);
}

describe('console timing rules', () => {
  it('keeps the next delay between 5 and 15 seconds inclusive over 1000 draws', () => {
    for (let i = 0; i < 1000; i++) {
      const delay = nextDelayMs(Math.random);
      expect(delay).toBeGreaterThanOrEqual(NEXT_DELAY_MIN_MS);
      expect(delay).toBeLessThanOrEqual(NEXT_DELAY_MAX_MS);
    }
  });

  it('reaches both bounds of the next delay', () => {
    expect(nextDelayMs(() => 0)).toBe(5000);
    expect(nextDelayMs(() => 0.99999999)).toBe(15000);
  });

  it('keeps the first delay between 0 and 5 seconds inclusive', () => {
    expect(firstDelayMs(() => 0)).toBe(0);
    expect(firstDelayMs(() => 0.99999999)).toBe(FIRST_DELAY_MAX_MS);
    for (let i = 0; i < 1000; i++) {
      const delay = firstDelayMs(Math.random);
      expect(delay).toBeGreaterThanOrEqual(0);
      expect(delay).toBeLessThanOrEqual(FIRST_DELAY_MAX_MS);
    }
  });

  it('never picks the previous index twice in a row', () => {
    let previous: number | null = null;
    for (let i = 0; i < 1000; i++) {
      const index = pickMessageIndex(Math.random, POOL.length, previous);
      expect(index).not.toBe(previous);
      expect(index).toBeGreaterThanOrEqual(0);
      expect(index).toBeLessThan(POOL.length);
      previous = index;
    }
  });

  it('steps past the previous index when the draw lands on it', () => {
    expect(pickMessageIndex(() => 0, 3, 0)).toBe(1);
    expect(pickMessageIndex(() => 0.99, 3, 2)).toBe(0);
    expect(pickMessageIndex(() => 0, 1, 0)).toBe(0);
  });
});

describe('console state transitions', () => {
  const empty: ConsoleState = { lines: [], lastIndex: null, nextId: 1 };
  const line = { timestamp: '[ 10:18:37 AM ] - ', text: 'Alpha', typedLength: 0 };

  it('appends lines with increasing ids and keeps the ring size', () => {
    let state = empty;
    for (let i = 0; i < CONSOLE_RING_SIZE + 5; i++) state = appendLine(state, line, i % 3);
    expect(state.lines.length).toBe(CONSOLE_RING_SIZE);
    expect(state.lines[0].id).toBe(6);
    expect(state.lines.at(-1)?.id).toBe(CONSOLE_RING_SIZE + 5);
    expect(state.nextId).toBe(CONSOLE_RING_SIZE + 6);
  });

  it('advances typing one character at a time and then returns the same state', () => {
    let state = appendLine(empty, line, 0);
    for (let i = 1; i <= line.text.length; i++) {
      state = advanceTyping(state, 1);
      expect(state.lines[0].typedLength).toBe(i);
    }
    expect(advanceTyping(state, 1)).toBe(state);
    expect(advanceTyping(state, 99)).toBe(state);
  });
});

describe('ConsoleScheduler', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.clearAllTimers();
    vi.useRealTimers();
  });

  it('shows the first line within 5 seconds, timestamp first', () => {
    const scheduler = setup({ random: () => 0.99999999 });
    scheduler.start();
    vi.advanceTimersByTime(FIRST_DELAY_MAX_MS - 1);
    expect(scheduler.lines().length).toBe(0);
    vi.advanceTimersByTime(1);
    expect(scheduler.lines().length).toBe(1);
    expect(scheduler.lines()[0].timestamp).toBe('[ 10:18:37 AM ] - ');
    scheduler.stop();
  });

  it('never shows the same message twice in a row', () => {
    const scheduler = setup({ random: () => 0 });
    scheduler.start();
    vi.advanceTimersByTime(0);
    for (let i = 0; i < 30; i++) {
      vi.advanceTimersByTime(NEXT_DELAY_MIN_MS);
      const lines = scheduler.lines();
      expect(lines.at(-1)?.text).not.toBe(lines.at(-2)?.text);
    }
    scheduler.stop();
  });

  it('keeps at most 12 lines and drops the oldest', () => {
    const scheduler = setup({ random: () => 0 });
    scheduler.start();
    vi.advanceTimersByTime(0);
    for (let i = 0; i < 30; i++) vi.advanceTimersByTime(NEXT_DELAY_MIN_MS);
    const lines = scheduler.lines();
    // 31 lines were shown (the first plus 30 more); only the last 12 remain.
    expect(lines.length).toBe(CONSOLE_RING_SIZE);
    expect(lines.at(-1)?.id).toBe(31);
    expect(lines[0].id).toBe(31 - CONSOLE_RING_SIZE + 1);
    scheduler.stop();
  });

  it('types a line one character per step with the timestamp shown at once', () => {
    const scheduler = setup({ random: () => 0 });
    scheduler.start();
    vi.advanceTimersByTime(0);
    const line = () => scheduler.lines()[0];
    expect(line().text).toBe('Alpha');
    expect(line().typedLength).toBe(0);
    expect(line().timestamp).toBe('[ 10:18:37 AM ] - ');
    vi.advanceTimersByTime(TYPING_STEP_MS);
    expect(line().typedLength).toBe(1);
    vi.advanceTimersByTime(TYPING_STEP_MS * 2);
    expect(line().typedLength).toBe(3);
    vi.advanceTimersByTime(TYPING_STEP_MS * 2);
    expect(line().typedLength).toBe(5);
    // Typing finished: only the timer for the next message remains.
    expect(vi.getTimerCount()).toBe(1);
    scheduler.stop();
  });

  it('shows the whole line at once under reduced motion', () => {
    const scheduler = setup({ random: () => 0, reducedMotion: true });
    scheduler.start();
    vi.advanceTimersByTime(0);
    expect(scheduler.lines()[0].typedLength).toBe('Alpha'.length);
    expect(vi.getTimerCount()).toBe(1);
    scheduler.stop();
  });

  it('does not schedule twice when started twice', () => {
    const scheduler = setup();
    scheduler.start();
    scheduler.start();
    expect(vi.getTimerCount()).toBe(1);
    scheduler.stop();
  });

  it('leaves no pending timers after stop()', () => {
    const scheduler = setup({ random: () => 0 });
    scheduler.start();
    vi.advanceTimersByTime(TYPING_STEP_MS);
    expect(vi.getTimerCount()).toBe(2);
    scheduler.stop();
    expect(vi.getTimerCount()).toBe(0);
    const shown = scheduler.lines();
    vi.advanceTimersByTime(NEXT_DELAY_MAX_MS * 2);
    expect(scheduler.lines()).toBe(shown);
  });
});
