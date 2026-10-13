import { TestBed } from '@angular/core/testing';
import { PlayerSession } from '../../core/session/player-session';
import {
  PLAYER_NAME_CREATE_FAILED_MESSAGE,
  PLAYER_NAME_REQUIRED_MESSAGE,
  PLAYER_NAME_TOO_LONG_MESSAGE,
  PlayerNameStore,
  validatePlayerName,
} from './player-name.store';

describe('validatePlayerName', () => {
  it('requires a name', () => {
    expect(validatePlayerName('')?.message).toBe(PLAYER_NAME_REQUIRED_MESSAGE);
  });

  it('treats a name of only spaces as missing', () => {
    expect(validatePlayerName('   ')?.message).toBe(PLAYER_NAME_REQUIRED_MESSAGE);
  });

  it('accepts 1 to 40 characters after trimming', () => {
    expect(validatePlayerName('A')).toBeUndefined();
    expect(validatePlayerName('x'.repeat(40))).toBeUndefined();
    expect(validatePlayerName(`  ${'x'.repeat(40)}  `)).toBeUndefined();
  });

  it('rejects more than 40 characters', () => {
    expect(validatePlayerName('x'.repeat(41))?.message).toBe(PLAYER_NAME_TOO_LONG_MESSAGE);
  });
});

describe('PlayerNameStore', () => {
  let store: PlayerNameStore;
  let createPlayer: ReturnType<typeof vi.fn<(name: string) => Promise<void>>>;

  beforeEach(() => {
    createPlayer = vi.fn<(name: string) => Promise<void>>();
    TestBed.configureTestingModule({
      providers: [PlayerNameStore, { provide: PlayerSession, useValue: { createPlayer } }],
    });
    store = TestBed.inject(PlayerNameStore);
  });

  function type(name: string): void {
    store.form.playerName().value.set(name);
  }

  it('starts empty, idle, without errors on screen', () => {
    expect(store.form.playerName().value()).toBe('');
    expect(store.submitting()).toBe(false);
    expect(store.error()).toBeNull();
    expect(store.nameError()).toBeNull();
  });

  it('shows the validation message and sends nothing when an invalid name is submitted', async () => {
    type('   ');

    expect(await store.submit()).toBe(false);

    expect(createPlayer).not.toHaveBeenCalled();
    expect(store.nameError()).toBe(PLAYER_NAME_REQUIRED_MESSAGE);
  });

  it('creates the player with the trimmed name and reports success', async () => {
    createPlayer.mockResolvedValue(undefined);
    type('  Admiral Nelson ');

    expect(await store.submit()).toBe(true);

    expect(createPlayer).toHaveBeenCalledExactlyOnceWith('Admiral Nelson');
    expect(store.error()).toBeNull();
  });

  it('is submitting while the request is pending and ignores a second submit', async () => {
    let finish!: () => void;
    createPlayer.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)));
    type('Admiral Nelson');

    const first = store.submit();
    expect(store.submitting()).toBe(true);
    expect(await store.submit()).toBe(false);
    expect(createPlayer).toHaveBeenCalledTimes(1);

    finish();
    expect(await first).toBe(true);
    expect(store.submitting()).toBe(false);
  });

  it('keeps the typed name and sets a retryable error when creation fails', async () => {
    createPlayer.mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(undefined);
    type('Admiral Nelson');

    expect(await store.submit()).toBe(false);
    expect(store.error()).toBe(PLAYER_NAME_CREATE_FAILED_MESSAGE);
    expect(store.form.playerName().value()).toBe('Admiral Nelson');
    expect(store.submitting()).toBe(false);

    expect(await store.submit()).toBe(true);
    expect(store.error()).toBeNull();
    expect(createPlayer).toHaveBeenCalledTimes(2);
  });
});
