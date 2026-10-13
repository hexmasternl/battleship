import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { firstValueFrom } from 'rxjs';
import { axe } from 'vitest-axe';
import { GAMES_URL } from '../games-api';
import { openCreateGameDialog } from './create-game-dialog';
import { CREATE_GAME_FAILED_MESSAGE } from './create-game-dialog.store';

const CREATED = { gameId: 'b0c5', gameCode: 'K7Q2M9XA' };

describe('CreateGameDialog', () => {
  let dialog: MatDialog;
  let backend: HttpTestingController;
  let opener: HTMLButtonElement;

  const settle = async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await TestBed.inject(ApplicationRef).whenStable();
  };
  const dialogElement = () => document.querySelector('mat-dialog-container') as HTMLElement | null;
  const buttons = () =>
    Array.from(
      dialogElement()?.querySelectorAll<HTMLButtonElement>('mat-dialog-actions button') ?? [],
    );
  const cancelButton = () => buttons()[0];
  const confirmButton = () => buttons()[1];
  const harness = async () =>
    TestbedHarnessEnvironment.documentRootLoader(TestBed.createComponent(Holder)).getHarness(
      MatDialogHarness,
    );

  /** Opens the dialog; the result is wrapped so awaiting open() does not await the close. */
  async function open(): Promise<{ result: Promise<{ gameCode: string } | null> }> {
    const result = openCreateGameDialog(dialog, opener);
    await firstValueFrom(dialog.openDialogs[0].afterOpened());
    await settle();
    return { result };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    dialog = TestBed.inject(MatDialog);
    backend = TestBed.inject(HttpTestingController);
    opener = document.createElement('button');
    opener.textContent = 'Create a new game';
    document.body.appendChild(opener);
    opener.focus();
  });

  afterEach(() => {
    dialog.closeAll();
    opener.remove();
    backend.verify();
  });

  it('asks to create a new game with a confirm and a cancel action, focus inside', async () => {
    await open();

    const dialogHarness = await harness();
    expect(await dialogHarness.getTitleText()).toBe('Create a new game');
    expect(await dialogHarness.getRole()).toBe('dialog');
    expect(cancelButton().textContent?.trim()).toBe('Cancel');
    expect(confirmButton().textContent?.trim()).toBe('Create game');
    expect(confirmButton().classList.contains('bs-btn--primary')).toBe(true);
    expect(dialogElement()?.contains(document.activeElement)).toBe(true);
    backend.expectNone(GAMES_URL);
  });

  it('creates the game and closes with its code', async () => {
    const { result } = await open();

    confirmButton().click();
    await settle();
    const req = backend.expectOne(GAMES_URL);
    expect(req.request.method).toBe('POST');
    req.flush(CREATED, { status: 201, statusText: 'Created' });

    expect(await result).toEqual({ gameCode: 'K7Q2M9XA' });
  });

  it('resolves null from cancel, sends nothing and returns focus to the opener', async () => {
    const { result } = await open();

    cancelButton().click();

    expect(await result).toBeNull();
    await settle();
    backend.expectNone(GAMES_URL);
    expect(document.activeElement).toBe(opener);
  });

  it('resolves null when closed with Escape', async () => {
    const { result } = await open();

    await (await harness()).close();

    expect(await result).toBeNull();
    backend.expectNone(GAMES_URL);
  });

  it('shows progress and disables both actions while creating; a second confirm sends nothing', async () => {
    await open();

    confirmButton().click();
    await settle();
    expect(confirmButton().disabled).toBe(true);
    expect(cancelButton().disabled).toBe(true);
    expect(dialogElement()?.querySelector('mat-progress-bar')).not.toBeNull();

    confirmButton().click();
    await settle();
    const req = backend.expectOne(GAMES_URL);
    req.flush(CREATED, { status: 201, statusText: 'Created' });
  });

  it('stays open with an error and creates the game on retry', async () => {
    const { result } = await open();

    confirmButton().click();
    await settle();
    backend.expectOne(GAMES_URL).flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(dialog.openDialogs.length).toBe(1);
    const alert = dialogElement()?.querySelector('[role=alert]');
    expect(alert?.textContent?.trim()).toBe(CREATE_GAME_FAILED_MESSAGE);
    expect(confirmButton().textContent?.trim()).toBe('Try again');
    expect(confirmButton().disabled).toBe(false);

    confirmButton().click();
    await settle();
    expect(dialogElement()?.querySelector('[role=alert]')).toBeNull();
    backend.expectOne(GAMES_URL).flush(CREATED, { status: 201, statusText: 'Created' });

    expect(await result).toEqual({ gameCode: 'K7Q2M9XA' });
  });

  it('has no accessibility violations, idle and after a failure', async () => {
    await open();
    const violations = async () =>
      (await axe(dialogElement() as HTMLElement)).violations.map(
        (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
      );

    expect(await violations()).toEqual([]);

    confirmButton().click();
    await settle();
    expect(await violations()).toEqual([]);
    backend.expectOne(GAMES_URL).flush(null, { status: 500, statusText: 'Server Error' });
    await settle();
    expect(await violations()).toEqual([]);
  }, 20000);
});

/** A throwaway host so the harness environment has a fixture to stabilise. */
@Component({ selector: 'battle-holder', template: '' })
class Holder {}
