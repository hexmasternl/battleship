import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { GamePlaceholder } from './game-placeholder';

describe('GamePlaceholder', () => {
  async function render(gameCode: string): Promise<HTMLElement> {
    await TestBed.configureTestingModule({
      imports: [GamePlaceholder],
      providers: [provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(GamePlaceholder);
    fixture.componentRef.setInput('gameCode', gameCode);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('names the game code as the heading of an info panel', async () => {
    const host = await render('K7Q2M9XA');
    const panel = host.querySelector('.bs-panel--info') as HTMLElement;
    expect(panel).not.toBeNull();
    expect(panel.querySelector('h1')?.textContent?.trim()).toBe('K7Q2M9XA');
    expect(panel.textContent).toContain('Game code');
  });

  it('links back to the landing page with a ghost button', async () => {
    const host = await render('K7Q2M9XA');
    const link = host.querySelector('a.bs-btn--ghost') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/');
  });

  it('has no accessibility violations', async () => {
    const host = await render('K7Q2M9XA');
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 20000);
});
