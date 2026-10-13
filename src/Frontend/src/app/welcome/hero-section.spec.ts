import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CONSOLE_MESSAGE_POOL, REDUCED_MOTION } from './console-scheduler';
import { HeroSection } from './hero-section';

describe('HeroSection', () => {
  let fixture: ComponentFixture<HeroSection>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HeroSection],
      providers: [
        provideRouter([]),
        { provide: REDUCED_MOTION, useValue: true },
        { provide: CONSOLE_MESSAGE_POOL, useValue: ['Radar sweep clear.'] },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(HeroSection);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  it('shows one level-one welcome heading and an intro paragraph', () => {
    const headings = host.querySelectorAll('h1');
    expect(headings.length).toBe(1);
    expect(headings[0].textContent?.trim()).toBe('Welcome aboard, Captain');
    expect(host.querySelector('.hero__intro')?.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('offers exactly two calls to action styled by role', () => {
    const links = host.querySelectorAll<HTMLAnchorElement>('.hero__actions a');
    expect(links.length).toBe(2);

    const [create, join] = Array.from(links);
    expect(create.textContent?.trim()).toBe('Create a new game');
    expect(create.getAttribute('href')).toBe('/games/new');
    expect(create.classList.contains('bs-btn')).toBe(true);
    expect(create.classList.contains('bs-btn--primary')).toBe(true);

    expect(join.textContent?.trim()).toBe('Join an existing game');
    expect(join.getAttribute('href')).toBe('/games/join');
    expect(join.classList.contains('bs-btn')).toBe(true);
    expect(join.classList.contains('bs-btn--secondary')).toBe(true);
  });

  it('places the welcome panel before the bridge console', () => {
    const jumbotron = host.querySelector('.hero__jumbotron') as HTMLElement;
    const children = Array.from(jumbotron.children);
    expect(children[0].classList.contains('hero__welcome')).toBe(true);
    expect(children[1].tagName.toLowerCase()).toBe('battle-bridge-console');
  });

  it('keeps the radar motif decorative', () => {
    const radar = host.querySelector('.hero__radar');
    expect(radar?.getAttribute('aria-hidden')).toBe('true');
    expect(host.querySelector('img')).toBeNull();
  });
});
