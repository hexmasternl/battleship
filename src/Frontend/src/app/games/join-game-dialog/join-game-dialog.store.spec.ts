import { TestBed } from '@angular/core/testing';
import { normalizeGameCode } from '../game-code';
import { GamesApi, JoinGameError, JoinGameFailure, JoinGameResponse } from '../games-api';
import {
  JOIN_GAME_FAILED_MESSAGE,
  JOIN_GAME_FULL_MESSAGE,
  JOIN_GAME_STARTED_MESSAGE,
  JoinGameState,
  JoinGameStore,
  codeEdited,
  initialJoinGameState,
  joinErrorMessage,
  joinFailed,
  joinStarted,
} from './join-game-dialog.store';

const JOINED: JoinGameResponse = { gameId: 'b0c5', gameCode: 'K7Q2M9XA' };

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

describe('normalizeGameCode', () => {
  it.each([
    ['k7q2m9xa', 'K7Q2M9XA'],
    ['k7q2-m9xa', 'K7Q2M9XA'],
    [' K7 Q2 M9 XA ', 'K7Q2M9XA'],
    ['K7Q2M9XA99', 'K7Q2M9XA'],
    ['é!?', ''],
  ])('turns %j into %j', (raw, expected) => {
    expect(normalizeGameCode(raw)).toBe(expected);
  });
});

describe('join-game transitions', () => {
  it('editing upper-cases the code and clears the error', () => {
    const failed: JoinGameState = { code: 'K7Q2M9XA', status: 'failed', error: 'full' };
    expect(codeEdited(failed, 'k7q2m9x')).toEqual({ code: 'K7Q2M9X', status: 'idle', error: null });
  });

  it('editing to the same code without an error returns the same state', () => {
    const state: JoinGameState = { code: 'K7Q2', status: 'idle', error: null };
    expect(codeEdited(state, 'k7q2')).toBe(state);
  });

  it('editing while joining returns the same state', () => {
    const joining: JoinGameState = { code: 'K7Q2M9XA', status: 'joining', error: null };
    expect(codeEdited(joining, 'AAAA')).toBe(joining);
  });

  it('starting clears the error of an earlier attempt and keeps the code', () => {
    const failed: JoinGameState = { code: 'K7Q2M9XA', status: 'failed', error: 'failed' };
    expect(joinStarted(failed)).toEqual({ code: 'K7Q2M9XA', status: 'joining', error: null });
  });

  it('starting while joining returns the same state', () => {
    const joining: JoinGameState = { code: 'K7Q2M9XA', status: 'joining', error: null };
    expect(joinStarted(joining)).toBe(joining);
  });

  it('failing records the reason and keeps the code', () => {
    const joining: JoinGameState = { code: 'K7Q2M9XA', status: 'joining', error: null };
    expect(joinFailed(joining, 'started')).toEqual({
      code: 'K7Q2M9XA',
      status: 'failed',
      error: 'started',
    });
  });

  it.each([
    ['not-found', 'No game with code K7Q2M9XA was found.'],
    ['full', JOIN_GAME_FULL_MESSAGE],
    ['started', JOIN_GAME_STARTED_MESSAGE],
    ['failed', JOIN_GAME_FAILED_MESSAGE],
  ] as const)('names %s as %j', (error, message) => {
    expect(joinErrorMessage(error, 'K7Q2M9XA')).toBe(message);
  });

  it('uses the design messages', () => {
    expect(JOIN_GAME_FULL_MESSAGE).toBe('This game already has two players.');
    expect(JOIN_GAME_STARTED_MESSAGE).toBe('This game has already started.');
    expect(JOIN_GAME_FAILED_MESSAGE).toBe('Something went wrong. Try again.');
  });
});

describe('JoinGameStore', () => {
  let joinGame: ReturnType<typeof vi.fn<(code: string) => Promise<JoinGameResponse>>>;
  let store: JoinGameStore;

  beforeEach(() => {
    joinGame = vi.fn<(code: string) => Promise<JoinGameResponse>>();
    TestBed.configureTestingModule({
      providers: [JoinGameStore, { provide: GamesApi, useValue: { joinGame } }],
    });
    store = TestBed.inject(JoinGameStore);
  });

  it('starts idle with an empty code, no error and join disabled', () => {
    expect(store.state()).toEqual(initialJoinGameState);
    expect(store.canJoin()).toBe(false);
    expect(store.errorMessage()).toBeNull();
  });

  it('upper-cases what is typed and enables join only for eight letters or digits', () => {
    store.edit('k7q2m9x');
    expect(store.code()).toBe('K7Q2M9X');
    expect(store.canJoin()).toBe(false);

    store.edit('k7q2m9xa');
    expect(store.code()).toBe('K7Q2M9XA');
    expect(store.canJoin()).toBe(true);
  });

  it('sends nothing without a valid code', async () => {
    store.edit('K7Q2');
    expect(await store.join()).toBeNull();
    expect(joinGame).not.toHaveBeenCalled();
  });

  it('is joining while the request is pending and resolves the joined game', async () => {
    const request = deferred<JoinGameResponse>();
    joinGame.mockReturnValue(request.promise);
    store.edit('K7Q2M9XA');

    const result = store.join();
    expect(store.isJoining()).toBe(true);
    expect(store.canJoin()).toBe(false);
    expect(joinGame).toHaveBeenCalledWith('K7Q2M9XA');

    request.resolve(JOINED);
    expect(await result).toEqual(JOINED);
    expect(store.status()).toBe('idle');
  });

  it('ignores a second join while the first is pending', async () => {
    const request = deferred<JoinGameResponse>();
    joinGame.mockReturnValue(request.promise);
    store.edit('K7Q2M9XA');

    const first = store.join();
    const second = store.join();

    expect(await second).toBeNull();
    request.resolve(JOINED);
    expect(await first).toEqual(JOINED);
    expect(joinGame).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['not-found', 'No game with code K7Q2M9XA was found.'],
    ['full', 'This game already has two players.'],
    ['started', 'This game has already started.'],
    ['failed', 'Something went wrong. Try again.'],
  ] as const)('keeps the code and names a %s refusal', async (reason: JoinGameError, message) => {
    joinGame.mockRejectedValueOnce(new JoinGameFailure(reason));
    store.edit('K7Q2M9XA');

    expect(await store.join()).toBeNull();

    expect(store.status()).toBe('failed');
    expect(store.error()).toBe(reason);
    expect(store.errorMessage()).toBe(message);
    expect(store.code()).toBe('K7Q2M9XA');
    expect(store.canJoin()).toBe(true);
  });

  it('treats an unexpected rejection as failed', async () => {
    joinGame.mockRejectedValueOnce(new Error('offline'));
    store.edit('K7Q2M9XA');

    await store.join();

    expect(store.error()).toBe('failed');
  });

  it('clears the error when the code is edited', async () => {
    joinGame.mockRejectedValueOnce(new JoinGameFailure('not-found'));
    store.edit('K7Q2M9XA');
    await store.join();

    store.edit('K7Q2M9XB');

    expect(store.error()).toBeNull();
    expect(store.errorMessage()).toBeNull();
    expect(store.status()).toBe('idle');
  });

  it('clears the error on a retry and resolves the joined game', async () => {
    joinGame.mockRejectedValueOnce(new JoinGameFailure('failed'));
    store.edit('K7Q2M9XA');
    await store.join();

    const retry = deferred<JoinGameResponse>();
    joinGame.mockReturnValueOnce(retry.promise);
    const result = store.join();
    expect(store.error()).toBeNull();
    retry.resolve(JOINED);

    expect(await result).toEqual(JOINED);
    expect(joinGame).toHaveBeenCalledTimes(2);
  });
});
