import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ComingSoon, ComingSoonFeature } from './coming-soon';

describe('ComingSoon', () => {
  async function render(feature: ComingSoonFeature): Promise<ComponentFixture<ComingSoon>> {
    await TestBed.configureTestingModule({
      imports: [ComingSoon],
      providers: [provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(ComingSoon);
    fixture.componentRef.setInput('feature', feature);
    await fixture.whenStable();
    return fixture;
  }

  it('names the join-game feature inside an info panel', async () => {
    const fixture = await render('join');
    const panel = fixture.nativeElement.querySelector('.bs-panel--info') as HTMLElement;
    expect(panel).not.toBeNull();
    expect(panel.querySelector('h1')?.textContent?.trim()).toBe('Join an existing game');
    expect(panel.textContent).toContain('Coming soon');
  });

  it('links back to the landing page with a ghost button', async () => {
    const fixture = await render('join');
    const link = fixture.nativeElement.querySelector('a.bs-btn--ghost') as HTMLAnchorElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('/');
    expect(link.classList.contains('bs-btn')).toBe(true);
  });
});
