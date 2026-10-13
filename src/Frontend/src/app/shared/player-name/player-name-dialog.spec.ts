import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { firstValueFrom } from 'rxjs';
import { axe } from 'vitest-axe';
import { MemoryStorage } from '../../../testing/memory-storage';
import { PERSISTENT_STORAGE, SESSION_STORAGE } from '../../core/session/token-storage';
import { openPlayerNameDialog } from './player-name-dialog';

const PAIR = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2999-01-01T00:00:00Z',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2999-01-01T00:00:00Z',
};

describe('PlayerNameDialog', () => {
  let dialog: MatDialog;
  let backend: HttpTestingController;
  let local: MemoryStorage;
  let opener: HTMLButtonElement;

  const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
  const overlay = () => document.querySelector('.cdk-overlay-container') as HTMLElement;
  const dialogElement = () => overlay().querySelector('mat-dialog-container') as HTMLElement | null;

  beforeEach(() => {
    local = new MemoryStorage();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PERSISTENT_STORAGE, useValue: () => local },
        { provide: SESSION_STORAGE, useValue: () => new MemoryStorage() },
      ],
    });
    dialog = TestBed.inject(MatDialog);
    backend = TestBed.inject(HttpTestingController);
    opener = document.createElement('button');
    opener.textContent = 'Opener';
    document.body.appendChild(opener);
    opener.focus();
  });

  afterEach(() => {
    dialog.closeAll();
    opener.remove();
    backend.verify();
  });

  it('opens a modal dialog with a heading, the player-name form and a cancel action', async () => {
    void openPlayerNameDialog(dialog, opener);
    await firstValueFrom(dialog.openDialogs[0].afterOpened());
    await settle();

    const loader = TestbedHarnessEnvironment.documentRootLoader(TestBed.createComponent(Holder));
    const harness = await loader.getHarness(MatDialogHarness);
    expect(await harness.getTitleText()).toBe('Choose your player name');
    expect(await harness.getRole()).toBe('dialog');
    expect(dialogElement()?.querySelector('battle-player-name')).not.toBeNull();
    expect(dialogElement()?.querySelector('mat-dialog-actions button')?.textContent?.trim()).toBe(
      'Cancel',
    );
    expect(dialogElement()?.contains(document.activeElement)).toBe(true);
  });

  it('resolves true once the player is created', async () => {
    const result = openPlayerNameDialog(dialog, opener);
    await settle();

    const input = dialogElement()?.querySelector('input') as HTMLInputElement;
    input.value = 'Admiral Nelson';
    input.dispatchEvent(new Event('input'));
    (dialogElement()?.querySelector('button[type=submit]') as HTMLButtonElement).click();
    await settle();
    backend.expectOne('/api/users/tokens').flush(PAIR, { status: 201, statusText: 'Created' });

    expect(await result).toBe(true);
  });

  it('resolves false from the cancel action and returns focus to the opener', async () => {
    const result = openPlayerNameDialog(dialog, opener);
    await settle();

    (dialogElement()?.querySelector('mat-dialog-actions button') as HTMLButtonElement).click();

    expect(await result).toBe(false);
    await settle();
    expect(document.activeElement).toBe(opener);
    expect(local.keys()).toEqual([]);
  });

  it('resolves false when closed with Escape', async () => {
    const result = openPlayerNameDialog(dialog, opener);
    await settle();

    const loader = TestbedHarnessEnvironment.documentRootLoader(TestBed.createComponent(Holder));
    await (await loader.getHarness(MatDialogHarness)).close();

    expect(await result).toBe(false);
  });

  it('has no accessibility violations', async () => {
    void openPlayerNameDialog(dialog, opener);
    await settle();

    const results = await axe(dialogElement() as HTMLElement);
    const summary = results.violations.map(
      (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
    );
    expect(summary).toEqual([]);
  }, 20000);
});

/** A throwaway host so the harness environment has a fixture to stabilise. */
@Component({ selector: 'battle-holder', template: '' })
class Holder {}
