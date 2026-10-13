import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { ApplicationRef, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { firstValueFrom } from 'rxjs';
import { axe } from 'vitest-axe';
import { GameOutcome } from '../game-page/game-page.store';
import {
  GAME_RESULT_CLOSED,
  GameResultClosed,
  GameResultDialog,
  openGameResultDialog,
} from './game-result-dialog';

/** A throwaway host so the harness environment has a fixture to stabilise. */
@Component({ template: '' })
class Holder {}

describe('GameResultDialog', () => {
  let dialog: MatDialog;

  const settle = async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await TestBed.inject(ApplicationRef).whenStable();
  };
  const container = () => document.querySelector('mat-dialog-container') as HTMLElement | null;
  const title = () => container()?.querySelector('[mat-dialog-title]')?.textContent?.trim();
  const message = () => container()?.querySelector('mat-dialog-content')?.textContent?.trim();
  const buttons = () => Array.from(container()?.querySelectorAll('button') ?? []);
  const harness = () =>
    TestbedHarnessEnvironment.documentRootLoader(TestBed.createComponent(Holder)).getHarness(
      MatDialogHarness,
    );

  async function open(
    outcome: GameOutcome,
  ): Promise<MatDialogRef<GameResultDialog, GameResultClosed>> {
    const ref = openGameResultDialog(dialog, outcome);
    await firstValueFrom(ref.afterOpened());
    await settle();
    return ref;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } }],
    });
    dialog = TestBed.inject(MatDialog);
  });

  afterEach(async () => {
    // The dialog closes only with its result, so a test that left it open closes it that way.
    dialog.openDialogs.forEach((ref) => ref.close(GAME_RESULT_CLOSED));
    await settle();
  });

  it.each([
    ['won', 'You won', 'enemy fleet is sunk'],
    ['lost', 'You lost', 'Your fleet is sunk'],
    ['neutral', 'The game has ended', 'This battle is over'],
  ] as const)('tells the %s outcome as "%s"', async (outcome, heading, text) => {
    await open(outcome);
    expect(title()).toBe(heading);
    expect(message()).toContain(text);
  });

  it('never tells a win or a loss for a game without a winner', async () => {
    await open('neutral');
    expect(container()?.textContent).not.toMatch(/You (won|lost)/);
  });

  it('is a modal dialog labelled by its title, with one primary Close button', async () => {
    const page = document.createElement('main');
    document.body.appendChild(page);
    try {
      await open('won');
      // Modal: a backdrop over the page and the page hidden from assistive technology.
      expect(document.querySelector('.cdk-overlay-backdrop')).not.toBeNull();
      expect(page.getAttribute('aria-hidden')).toBe('true');
    } finally {
      page.remove();
    }
    const host = container();
    expect(host?.getAttribute('role')).toBe('dialog');
    const labelledBy = host?.getAttribute('aria-labelledby');
    expect(labelledBy && document.getElementById(labelledBy)?.textContent?.trim()).toBe('You won');
    expect(buttons().map((b) => b.textContent?.trim())).toEqual(['Close']);
    expect(buttons()[0].classList).toContain('bs-btn--primary');
  });

  it('moves the focus to Close when it opens', async () => {
    await open('lost');
    expect(document.activeElement).toBe(buttons()[0]);
    expect(buttons()[0].hasAttribute('cdkFocusInitial')).toBe(true);
  });

  it('does not disable closing as a whole', async () => {
    const ref = await open('won');
    expect(ref.disableClose).toBeFalsy();
  });

  it('closes with its result on Close', async () => {
    const ref = await open('won');
    const closed = firstValueFrom(ref.afterClosed());
    buttons()[0].click();
    await settle();
    expect(await closed).toBe(GAME_RESULT_CLOSED);
    expect(container()).toBeNull();
  });

  it('closes with its result on Escape', async () => {
    const ref = await open('lost');
    const closed = firstValueFrom(ref.afterClosed());
    await (await harness()).close();
    await settle();
    expect(await closed).toBe(GAME_RESULT_CLOSED);
    expect(container()).toBeNull();
  });

  it('stays open on a click on the backdrop', async () => {
    await open('won');
    (document.querySelector('.cdk-overlay-backdrop') as HTMLElement).click();
    await settle();
    expect(container()).not.toBeNull();
    expect(dialog.openDialogs.length).toBe(1);
  });

  it('has no accessibility violations', async () => {
    await open('won');
    expect((await axe(container() as HTMLElement)).violations.map((v) => v.id)).toEqual([]);
  });
});
