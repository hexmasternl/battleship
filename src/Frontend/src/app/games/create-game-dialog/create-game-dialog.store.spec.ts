import { TestBed } from '@angular/core/testing';
import { CreateGameResponse, GamesApi } from '../games-api';
import {
  CREATE_GAME_FAILED_MESSAGE,
  CreateGameState,
  CreateGameStore,
  creationFailed,
  creationStarted,
} from './create-game-dialog.store';

const CREATED: CreateGameResponse = { gameId: 'b0c5', gameCode: 'K7Q2M9XA' };

/** A promise with its settle functions exposed, so a test decides when the request answers. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe('create-game transitions', () => {
  it('starting clears the error of an earlier attempt', () => {
    const failed: CreateGameState = { status: 'failed', error: 'x' };
    expect(creationStarted(failed)).toEqual({ status: 'creating', error: null });
  });

  it('starting while creating returns the same state', () => {
    const creating: CreateGameState = { status: 'creating', error: null };
    expect(creationStarted(creating)).toBe(creating);
  });

  it('failing records the message', () => {
    expect(creationFailed({ status: 'creating', error: null }, 'boom')).toEqual({
      status: 'failed',
      error: 'boom',
    });
  });
});

describe('CreateGameStore', () => {
  let createGame: ReturnType<typeof vi.fn<() => Promise<CreateGameResponse>>>;
  let store: CreateGameStore;

  beforeEach(() => {
    createGame = vi.fn<() => Promise<CreateGameResponse>>();
    TestBed.configureTestingModule({
      providers: [CreateGameStore, { provide: GamesApi, useValue: { createGame } }],
    });
    store = TestBed.inject(CreateGameStore);
  });

  it('starts idle without an error', () => {
    expect(store.status()).toBe('idle');
    expect(store.error()).toBeNull();
  });

  it('is creating while the request is pending and resolves the created game', async () => {
    const request = deferred<CreateGameResponse>();
    createGame.mockReturnValue(request.promise);

    const result = store.create();
    expect(store.isCreating()).toBe(true);

    request.resolve(CREATED);
    expect(await result).toEqual(CREATED);
    expect(store.status()).toBe('idle');
  });

  it('ignores a second call while the first is pending', async () => {
    const request = deferred<CreateGameResponse>();
    createGame.mockReturnValue(request.promise);

    const first = store.create();
    const second = store.create();

    expect(await second).toBeNull();
    request.resolve(CREATED);
    expect(await first).toEqual(CREATED);
    expect(createGame).toHaveBeenCalledTimes(1);
  });

  it('records a retryable error when the request fails, and clears it on the retry', async () => {
    createGame.mockRejectedValueOnce(new Error('offline'));

    expect(await store.create()).toBeNull();
    expect(store.hasFailed()).toBe(true);
    expect(store.error()).toBe(CREATE_GAME_FAILED_MESSAGE);

    const retry = deferred<CreateGameResponse>();
    createGame.mockReturnValueOnce(retry.promise);
    const result = store.create();
    expect(store.error()).toBeNull();
    retry.resolve(CREATED);
    expect(await result).toEqual(CREATED);
  });
});
