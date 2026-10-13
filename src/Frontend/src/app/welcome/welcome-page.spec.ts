import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { CONSOLE_MESSAGE_POOL, CONSOLE_RANDOM, REDUCED_MOTION } from './console-scheduler';
import { WelcomePage } from './welcome-page';

describe('WelcomePage', () => {
  let fixture: ComponentFixture<WelcomePage>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WelcomePage],
      providers: [
        provideRouter([]),
        { provide: REDUCED_MOTION, useValue: true },
        // Draw 0 so the first console line is on screen at once and is part of the axe run.
        { provide: CONSOLE_RANDOM, useValue: () => 0 },
        { provide: CONSOLE_MESSAGE_POOL, useValue: ['Radar sweep clear.', 'Sonar ping sent.'] },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(WelcomePage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  it('composes the hero and the information section inside a main landmark', () => {
    const main = host.querySelector('main');
    expect(main).not.toBeNull();
    expect(main?.querySelector('battle-hero-section')).not.toBeNull();
    expect(main?.querySelector('battle-info-section')).not.toBeNull();
    expect(host.querySelectorAll('h1').length).toBe(1);
  });

  it('has no accessibility violations', async () => {
    // Let the first console line land (0ms draw), then run axe on the attached fixture.
    // jsdom cannot resolve colours from custom properties, so contrast is covered by the
    // throwaway script recorded in README.md rather than by this run.
    await new Promise((resolve) => setTimeout(resolve, 0));
    await fixture.whenStable();
    expect(host.querySelector('.console__line')).not.toBeNull();

    const results = await axe(host);
    const summary = results.violations.map(
      (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
    );
    expect(summary).toEqual([]);
  }, 20000);
});
