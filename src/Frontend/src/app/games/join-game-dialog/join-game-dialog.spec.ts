import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { firstValueFrom } from 'rxjs';
import { axe } from 'vitest-axe';
import { joinGameUrl } from '../games-api';
import { openJoinGameDialog } from './join-game-dialog';

const JOINED = { gameId: 'b0c5', gameCode: 'K7Q2M9XA' };
const JOIN_URL = joinGameUrl('K7Q2M9XA');
const problem = (slug: string) => ({
  type: `https://battleship.hexmaster.nl/problems/${slug}`,
  title: 'Refused',
});

describe('JoinGameDialog', () => {
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
  const joinButton = () => buttons()[1];
  const codeInput = () => dialogElement()?.querySelector('input') as HTMLInputElement;
  const alert = () => dialogElement()?.querySelector('[role=alert]') ?? null;
  const harness = async () =>
    TestbedHarnessEnvironment.documentRootLoader(TestBed.createComponent(Holder)).getHarness(
      MatDialogHarness,
    );

  /** Opens the dialog; the result is wrapped so awaiting open() does not await the close. */
  async function open(): Promise<{ result: Promise<{ gameCode: string } | null> }> {
    const result = openJoinGameDialog(dialog, opener);
    await firstValueFrom(dialog.openDialogs[0].afterOpened());
    await settle();
    return { result };
  }

  async function type(value: string): Promise<void> {
    codeInput().value = value;
    codeInput().dispatchEvent(new Event('input'));
    await settle();
  }

  /** Types a code, joins and answers the request with an error. */
  async function refuse(status: number, body: object | null): Promise<void> {
    await type('k7q2m9xa');
    joinButton().click();
    await settle();
    backend.expectOne(JOIN_URL).flush(body, { status, statusText: 'Refused' });
    await settle();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    dialog = TestBed.inject(MatDialog);
    backend = TestBed.inject(HttpTestingController);
    opener = document.createElement('button');
    opener.textContent = 'Join an existing game';
    document.body.appendChild(opener);
    opener.focus();
  });

  afterEach(() => {
    dialog.closeAll();
    opener.remove();
    backend.verify();
  });

  it('asks for a game code with a join and a cancel action, focus in the code input', async () => {
    await open();

    const dialogHarness = await harness();
    expect(await dialogHarness.getTitleText()).toBe('Join an existing game');
    expect(await dialogHarness.getRole()).toBe('dialog');
    expect(cancelButton().textContent?.trim()).toBe('Cancel');
    expect(joinButton().textContent?.trim()).toBe('Join game');
    expect(joinButton().classList.contains('bs-btn--primary')).toBe(true);
    // CDK focuses the container at once and the code input after the next render.
    await vi.waitFor(() => expect(document.activeElement).toBe(codeInput()));

    const input = codeInput();
    expect(dialogElement()?.querySelector(`label[for="${input.id}"]`)?.textContent?.trim()).toBe(
      'Game code',
    );
    expect(input.getAttribute('autocapitalize')).toBe('characters');
    expect(input.getAttribute('autocomplete')).toBe('off');
    expect(input.getAttribute('spellcheck')).toBe('false');
    backend.expectNone(() => true);
  });

  it('shows the typed code in upper case, stripped and limited to eight characters', async () => {
    await open();

    await type('k7q2m9xa');
    expect(codeInput().value).toBe('K7Q2M9XA');

    await type('k7q2-m9xa-zz');
    expect(codeInput().value).toBe('K7Q2M9XA');
  });

  it('keeps join disabled until the code is exactly eight letters or digits', async () => {
    await open();
    expect(joinButton().disabled).toBe(true);

    await type('k7q2m9x');
    expect(joinButton().disabled).toBe(true);

    codeInput().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await settle();
    backend.expectNone(() => true);

    await type('k7q2m9xa');
    expect(joinButton().disabled).toBe(false);
  });

  it('joins the game and closes with its code', async () => {
    const { result } = await open();

    await type('k7q2m9xa');
    joinButton().click();
    await settle();
    const req = backend.expectOne(JOIN_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(JOINED);

    expect(await result).toEqual({ gameCode: 'K7Q2M9XA' });
  });

  it('joins with the Enter key', async () => {
    const { result } = await open();

    await type('K7Q2M9XA');
    codeInput().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await settle();
    backend.expectOne(JOIN_URL).flush(JOINED);

    expect(await result).toEqual({ gameCode: 'K7Q2M9XA' });
  });

  it('resolves null from cancel, sends nothing and returns focus to the opener', async () => {
    const { result } = await open();
    await type('K7Q2M9XA');

    cancelButton().click();

    expect(await result).toBeNull();
    await settle();
    backend.expectNone(() => true);
    expect(document.activeElement).toBe(opener);
  });

  it('resolves null when closed with Escape', async () => {
    const { result } = await open();

    await (await harness()).close();

    expect(await result).toBeNull();
    backend.expectNone(() => true);
  });

  it('shows progress and disables both actions while joining; a second join sends nothing', async () => {
    await open();
    await type('K7Q2M9XA');

    joinButton().click();
    await settle();
    expect(joinButton().disabled).toBe(true);
    expect(cancelButton().disabled).toBe(true);
    expect(codeInput().readOnly).toBe(true);
    expect(dialogElement()?.querySelector('mat-progress-bar')).not.toBeNull();

    joinButton().click();
    codeInput().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await settle();
    backend.expectOne(JOIN_URL).flush(JOINED);
  });

  it.each([
    [404, problem('game-not-found'), 'No game with code K7Q2M9XA was found.'],
    [409, problem('game-full'), 'This game already has two players.'],
    [409, problem('game-already-started'), 'This game has already started.'],
    [409, problem('game-update-conflict'), 'Something went wrong. Try again.'],
    [500, null, 'Something went wrong. Try again.'],
  ])(
    'stays open after a %d and names the reason, keeping the code',
    async (status, body, message) => {
      const { result } = await open();
      let settled = false;
      void result.then(() => (settled = true));

      await refuse(status, body);

      expect(settled).toBe(false);
      expect(dialog.openDialogs.length).toBe(1);
      expect(alert()?.textContent?.trim()).toBe(message);
      expect(codeInput().value).toBe('K7Q2M9XA');
      expect(codeInput().getAttribute('aria-invalid')).toBe('true');
      expect(codeInput().getAttribute('aria-describedby')).toContain(alert()?.id);
      expect(document.activeElement).toBe(codeInput());
      expect(joinButton().disabled).toBe(false);
    },
  );

  it('stays open after a network error', async () => {
    await open();
    await type('K7Q2M9XA');

    joinButton().click();
    await settle();
    backend.expectOne(JOIN_URL).error(new ProgressEvent('error'));
    await settle();

    expect(dialog.openDialogs.length).toBe(1);
    expect(alert()?.textContent?.trim()).toBe('Something went wrong. Try again.');
  });

  it('clears the error when the code is edited', async () => {
    await open();
    await refuse(404, problem('game-not-found'));

    await type('K7Q2M9XB');

    expect(alert()).toBeNull();
    expect(codeInput().getAttribute('aria-invalid')).toBe('false');
  });

  it('joins on retry after a failure', async () => {
    const { result } = await open();
    await refuse(409, problem('game-update-conflict'));

    joinButton().click();
    await settle();
    expect(alert()).toBeNull();
    backend.expectOne(JOIN_URL).flush(JOINED);

    expect(await result).toEqual({ gameCode: 'K7Q2M9XA' });
  });

  it('has no accessibility violations, idle, joining and after a refusal', async () => {
    await open();
    const violations = async () =>
      (await axe(dialogElement() as HTMLElement)).violations.map(
        (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
      );

    expect(await violations()).toEqual([]);

    await type('K7Q2M9XA');
    joinButton().click();
    await settle();
    expect(await violations()).toEqual([]);
    backend
      .expectOne(JOIN_URL)
      .flush(problem('game-full'), { status: 409, statusText: 'Conflict' });
    await settle();
    expect(await violations()).toEqual([]);
  }, 20000);
});

/** A throwaway host so the harness environment has a fixture to stabilise. */
@Component({ selector: 'battle-holder', template: '' })
class Holder {}
