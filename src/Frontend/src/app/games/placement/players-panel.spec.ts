import { TestBed } from '@angular/core/testing';
import { axe } from 'vitest-axe';
import { me, opponent } from '../../../testing/game-views';
import { OpponentView, PlayerView } from '../game-view';
import { PlayersPanel } from './players-panel';

describe('PlayersPanel', () => {
  async function render(caller: PlayerView | null, other: OpponentView | null) {
    const fixture = TestBed.createComponent(PlayersPanel);
    fixture.componentRef.setInput('me', caller);
    fixture.componentRef.setInput('opponent', other);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const rows = () =>
      Array.from(host.querySelectorAll<HTMLElement>('.players__row')).map((r) => ({
        name: r.querySelector('.players__name')?.textContent?.replace(/\s+/g, ' ').trim(),
        status: r.querySelector('.players__status')?.textContent?.replace(/\s+/g, ' ').trim(),
        absent: r.classList.contains('is-absent'),
      }));
    return { fixture, host, rows };
  }

  it('shows the caller, not ready, and a greyed-out Player 2 while alone', async () => {
    const { rows } = await render(me(), null);
    expect(rows()).toEqual([
      { name: 'Ann You', status: 'hourglass_empty Not ready', absent: false },
      { name: 'Player 2', status: 'person_add Waiting to join', absent: true },
    ]);
  });

  it('shows the opponent by name with their ready state', async () => {
    const { rows } = await render(me({ ready: true }), opponent());
    expect(rows()).toEqual([
      { name: 'Ann You', status: 'check_circle Ready', absent: false },
      { name: 'Bob', status: 'hourglass_empty Not ready', absent: false },
    ]);
  });

  it('follows a change of the opponent to ready', async () => {
    const { fixture, rows } = await render(me(), opponent());
    fixture.componentRef.setInput('opponent', opponent({ ready: true }));
    await fixture.whenStable();
    expect(rows()[1].status).toBe('check_circle Ready');
  });

  it('has no accessibility violations', async () => {
    const { host } = await render(me(), null);
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 20000);
});
