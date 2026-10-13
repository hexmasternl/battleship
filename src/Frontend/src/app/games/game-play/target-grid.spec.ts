import { TestBed } from '@angular/core/testing';
import { ShotView } from '../game-view';
import { Cell } from '../placement/placement-rules';
import { TargetGrid } from './target-grid';

describe('TargetGrid', () => {
  async function render(
    inputs: { shots?: readonly ShotView[]; target?: Cell | null; canShoot?: boolean } = {},
  ) {
    TestBed.configureTestingModule({ imports: [TargetGrid] });
    const fixture = TestBed.createComponent(TargetGrid);
    fixture.componentRef.setInput('shots', inputs.shots ?? []);
    fixture.componentRef.setInput('target', inputs.target ?? null);
    fixture.componentRef.setInput('canShoot', inputs.canShoot ?? true);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const selected: Cell[] = [];
    fixture.componentInstance.cellSelected.subscribe((cell) => selected.push(cell));
    let cleared = 0;
    fixture.componentInstance.cleared.subscribe(() => cleared++);
    const cell = (label: string) =>
      host.querySelector<HTMLButtonElement>(`button[aria-label^="${label}"]`) as HTMLButtonElement;
    return { fixture, host, selected, cell, clearedCount: () => cleared };
  }

  it('shows a 12 x 12 grid of buttons labelled with their coordinates', async () => {
    const { host } = await render();
    const buttons = host.querySelectorAll('button');
    expect(buttons.length).toBe(144);
    expect(buttons[0].getAttribute('aria-label')).toBe('A1');
    expect(buttons[143].getAttribute('aria-label')).toBe('L12');
    expect(host.querySelectorAll('[role="columnheader"]').length).toBe(13);
  });

  it('names hits and misses and draws their markers', async () => {
    const { host } = await render({
      shots: [
        { column: 4, row: 6, hit: true },
        { column: 0, row: 0, hit: false },
      ],
    });
    expect(host.querySelector('[aria-label="E7, hit"] .shot-marker.is-hit')).not.toBeNull();
    expect(host.querySelector('[aria-label="A1, miss"] .shot-marker.is-miss')).not.toBeNull();
  });

  it('emits a selectable cell and not one already fired at', async () => {
    const { selected, cell } = await render({ shots: [{ column: 0, row: 0, hit: false }] });
    cell('A1').click();
    cell('B2').click();
    expect(selected).toEqual([{ column: 1, row: 1 }]);
    expect(cell('A1').getAttribute('aria-disabled')).toBe('true');
    expect(cell('B2').getAttribute('aria-disabled')).toBeNull();
  });

  it('disables every cell and emits nothing while the caller may not shoot', async () => {
    const { host, selected, cell } = await render({ canShoot: false });
    cell('E7').click();
    expect(selected).toEqual([]);
    expect(host.querySelectorAll('button[aria-disabled="true"]').length).toBe(144);
  });

  it('marks the target and gives it the tab stop', async () => {
    const { host, cell } = await render({ target: { column: 4, row: 6 } });
    expect(cell('E7').getAttribute('aria-label')).toBe('E7, target');
    expect(cell('E7').classList).toContain('is-target');
    expect(cell('E7').tabIndex).toBe(0);
    expect(host.querySelectorAll('.is-target').length).toBe(1);
    expect(host.querySelectorAll('button[tabindex="0"]').length).toBe(1);
  });

  it('moves the focus with the arrow keys and clears on Escape', async () => {
    const { fixture, host, cell, clearedCount } = await render();
    document.body.appendChild(host);
    try {
      cell('A1').focus();
      cell('A1').dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
      await fixture.whenStable();
      expect(document.activeElement).toBe(cell('B1'));
      expect(cell('B1').tabIndex).toBe(0);

      cell('B1').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      expect(clearedCount()).toBe(1);
    } finally {
      host.remove();
    }
  });
});
