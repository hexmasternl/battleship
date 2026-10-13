import { TestBed } from '@angular/core/testing';
import { ShotMarker } from './shot-marker';

describe('ShotMarker', () => {
  async function render(hit: boolean) {
    TestBed.configureTestingModule({ imports: [ShotMarker] });
    const fixture = TestBed.createComponent(ShotMarker);
    fixture.componentRef.setInput('hit', hit);
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  it('is a hit marker for a hit, hidden from assistive technology', async () => {
    const { host } = await render(true);
    expect(host.classList).toContain('shot-marker');
    expect(host.classList).toContain('is-hit');
    expect(host.classList).not.toContain('is-miss');
    expect(host.getAttribute('aria-hidden')).toBe('true');
  });

  it('is a miss marker for a miss', async () => {
    const { host } = await render(false);
    expect(host.classList).toContain('is-miss');
    expect(host.classList).not.toContain('is-hit');
  });
});
