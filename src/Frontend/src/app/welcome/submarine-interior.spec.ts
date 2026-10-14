import { ComponentFixture, TestBed } from '@angular/core/testing';
import { REDUCED_MOTION } from './console-scheduler';
import { SubmarineInterior } from './submarine-interior';

describe('SubmarineInterior', () => {
  async function render(reducedMotion: boolean): Promise<ComponentFixture<SubmarineInterior>> {
    await TestBed.configureTestingModule({
      imports: [SubmarineInterior],
      providers: [{ provide: REDUCED_MOTION, useValue: reducedMotion }],
    }).compileComponents();
    const fixture = TestBed.createComponent(SubmarineInterior);
    await fixture.whenStable();
    return fixture;
  }

  it('renders one full-HD drawing cropped like a cover image, hidden from assistive technology', async () => {
    const fixture = await render(false);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.getAttribute('aria-hidden')).toBe('true');
    expect(host.classList.contains('submarine')).toBe(true);

    const svgs = host.querySelectorAll('svg');
    expect(svgs.length).toBe(1);
    expect(svgs[0].getAttribute('viewBox')).toBe('0 0 1920 1080');
    expect(svgs[0].getAttribute('preserveAspectRatio')).toBe('xMidYMid slice');
    expect(svgs[0].getAttribute('focusable')).toBe('false');
    expect(host.querySelector('img')).toBeNull();
  });

  it('draws every colour as a design system token', async () => {
    const fixture = await render(false);
    const host = fixture.nativeElement as HTMLElement;
    const styled = Array.from(host.querySelectorAll<SVGElement>('[style]'));
    const colourValues = styled
      .flatMap((element) => ['fill', 'stroke', 'stop-color'].map((p) => element.style.getPropertyValue(p)))
      .filter((value) => value !== '' && value !== 'none' && !value.startsWith('url('));
    expect(colourValues.length).toBeGreaterThan(100);
    expect(colourValues.every((value) => value.startsWith('var(--bs-color-'))).toBe(true);
  });

  it('animates lamps, needles, steam, the radar sweep and the sonar ping', async () => {
    const fixture = await render(false);
    const host = fixture.nativeElement as HTMLElement;
    for (const motion of ['sub-on', 'sub-needle', 'sub-steam', 'sub-spin', 'sub-ping', 'sub-bar']) {
      expect(host.querySelectorAll(`.${motion}`).length, motion).toBeGreaterThan(0);
    }
    expect(host.classList.contains('submarine--reduced-motion')).toBe(false);
  });

  it('marks the host when reduced motion is requested', async () => {
    const fixture = await render(true);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.classList.contains('submarine--reduced-motion')).toBe(true);
  });
});
