import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BridgeConsole } from './bridge-console';
import {
  CONSOLE_CLOCK,
  CONSOLE_MESSAGE_POOL,
  CONSOLE_RANDOM,
  ConsoleScheduler,
  NEXT_DELAY_MAX_MS,
  REDUCED_MOTION,
} from './console-scheduler';

const MESSAGE = 'Sonar contact bearing 047, range 3200 yards.';

describe('BridgeConsole', () => {
  let fixture: ComponentFixture<BridgeConsole>;

  beforeEach(async () => {
    // Fake timers drive the scheduler, so change detection is run by hand after each advance
    // instead of awaiting stability (which would wait on the faked clock).
    vi.useFakeTimers();
    await TestBed.configureTestingModule({
      imports: [BridgeConsole],
      providers: [
        { provide: REDUCED_MOTION, useValue: true },
        { provide: CONSOLE_RANDOM, useValue: () => 0 },
        { provide: CONSOLE_CLOCK, useValue: () => new Date(2026, 9, 13, 10, 18, 37) },
        { provide: CONSOLE_MESSAGE_POOL, useValue: [MESSAGE, 'Radar sweep clear.'] },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(BridgeConsole);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.clearAllTimers();
    vi.useRealTimers();
  });

  it('renders as a design system panel with an empty screen before the first line', () => {
    const host = fixture.nativeElement as HTMLElement;
    expect(host.classList.contains('bs-panel')).toBe(true);
    expect(host.querySelectorAll('.console__line').length).toBe(0);
  });

  it('shows the whole first line at once under reduced motion', () => {
    vi.advanceTimersByTime(0);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const line = host.querySelector('.console__line') as HTMLElement;
    expect(line.textContent).toBe(`[ 10:18:37 AM ] - ${MESSAGE}`);
    expect(line.classList.contains('console__line--typing')).toBe(false);
    expect(host.querySelector('.console__caret')).not.toBeNull();
  });

  it('announces complete lines through a polite live region', () => {
    vi.advanceTimersByTime(0);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const region = host.querySelector('[aria-live="polite"]') as HTMLElement;
    expect(region).not.toBeNull();
    expect(region.getAttribute('aria-atomic')).toBe('false');
    expect(region.textContent?.trim()).toBe(`[ 10:18:37 AM ] - ${MESSAGE}`);
    expect(host.querySelector('.console__screen')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('stops scheduling when destroyed', () => {
    const scheduler = fixture.debugElement.injector.get(ConsoleScheduler);
    const stop = vi.spyOn(scheduler, 'stop');
    vi.advanceTimersByTime(0);
    const shown = scheduler.lines();
    expect(shown.length).toBe(1);
    fixture.destroy();
    expect(stop).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(NEXT_DELAY_MAX_MS * 2);
    expect(scheduler.lines()).toBe(shown);
  });
});
