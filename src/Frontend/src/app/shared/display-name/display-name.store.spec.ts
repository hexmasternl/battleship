import { TestBed } from '@angular/core/testing';
import { UserSession } from '../../core/session/user-session';
import {
  DISPLAY_NAME_CREATE_FAILED_MESSAGE,
  DISPLAY_NAME_REQUIRED_MESSAGE,
  DISPLAY_NAME_TOO_LONG_MESSAGE,
  DisplayNameStore,
  validateDisplayName,
} from './display-name.store';

describe('validateDisplayName', () => {
  it('requires a name', () => {
    expect(validateDisplayName('')?.message).toBe(DISPLAY_NAME_REQUIRED_MESSAGE);
  });

  it('treats a name of only spaces as missing', () => {
    expect(validateDisplayName('   ')?.message).toBe(DISPLAY_NAME_REQUIRED_MESSAGE);
  });

  it('accepts 1 to 40 characters after trimming', () => {
    expect(validateDisplayName('A')).toBeUndefined();
    expect(validateDisplayName('x'.repeat(40))).toBeUndefined();
    expect(validateDisplayName(`  ${'x'.repeat(40)}  `)).toBeUndefined();
  });

  it('rejects more than 40 characters', () => {
    expect(validateDisplayName('x'.repeat(41))?.message).toBe(DISPLAY_NAME_TOO_LONG_MESSAGE);
  });
});

describe('DisplayNameStore', () => {
  let store: DisplayNameStore;
  let createUser: ReturnType<typeof vi.fn<(name: string) => Promise<void>>>;

  beforeEach(() => {
    createUser = vi.fn<(name: string) => Promise<void>>();
    TestBed.configureTestingModule({
      providers: [DisplayNameStore, { provide: UserSession, useValue: { createUser } }],
    });
    store = TestBed.inject(DisplayNameStore);
  });

  function type(name: string): void {
    store.form.displayName().value.set(name);
  }

  it('starts empty, idle, without errors on screen', () => {
    expect(store.form.displayName().value()).toBe('');
    expect(store.submitting()).toBe(false);
    expect(store.error()).toBeNull();
    expect(store.nameError()).toBeNull();
  });

  it('shows the validation message and sends nothing when an invalid name is submitted', async () => {
    type('   ');

    expect(await store.submit()).toBe(false);

    expect(createUser).not.toHaveBeenCalled();
    expect(store.nameError()).toBe(DISPLAY_NAME_REQUIRED_MESSAGE);
  });

  it('creates the user with the trimmed name and reports success', async () => {
    createUser.mockResolvedValue(undefined);
    type('  Admiral Nelson ');

    expect(await store.submit()).toBe(true);

    expect(createUser).toHaveBeenCalledExactlyOnceWith('Admiral Nelson');
    expect(store.error()).toBeNull();
  });

  it('is submitting while the request is pending and ignores a second submit', async () => {
    let finish!: () => void;
    createUser.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)));
    type('Admiral Nelson');

    const first = store.submit();
    expect(store.submitting()).toBe(true);
    expect(await store.submit()).toBe(false);
    expect(createUser).toHaveBeenCalledTimes(1);

    finish();
    expect(await first).toBe(true);
    expect(store.submitting()).toBe(false);
  });

  it('keeps the typed name and sets a retryable error when creation fails', async () => {
    createUser.mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(undefined);
    type('Admiral Nelson');

    expect(await store.submit()).toBe(false);
    expect(store.error()).toBe(DISPLAY_NAME_CREATE_FAILED_MESSAGE);
    expect(store.form.displayName().value()).toBe('Admiral Nelson');
    expect(store.submitting()).toBe(false);

    expect(await store.submit()).toBe(true);
    expect(store.error()).toBeNull();
    expect(createUser).toHaveBeenCalledTimes(2);
  });
});
