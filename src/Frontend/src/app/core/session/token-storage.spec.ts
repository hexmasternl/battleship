import { TestBed } from '@angular/core/testing';
import {
  ACCESS_TOKEN_EXPIRES_AT_KEY,
  ACCESS_TOKEN_KEY,
  PERSISTENT_STORAGE,
  REFRESH_TOKEN_EXPIRES_AT_KEY,
  REFRESH_TOKEN_KEY,
  SESSION_STORAGE,
  TokenPair,
  TokenStorage,
} from './token-storage';
import { MemoryStorage } from '../../../testing/memory-storage';

const PAIR: TokenPair = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2026-10-13T10:15:00Z',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2027-01-11T10:00:00Z',
};

function setup(persistent: () => Storage, session: () => Storage): TokenStorage {
  TestBed.configureTestingModule({
    providers: [
      { provide: PERSISTENT_STORAGE, useValue: persistent },
      { provide: SESSION_STORAGE, useValue: session },
    ],
  });
  return TestBed.inject(TokenStorage);
}

describe('TokenStorage', () => {
  let local: MemoryStorage;
  let session: MemoryStorage;
  let storage: TokenStorage;

  beforeEach(() => {
    local = new MemoryStorage();
    session = new MemoryStorage();
    storage = setup(
      () => local,
      () => session,
    );
  });

  it('puts the refresh token in localStorage and the access token in sessionStorage', () => {
    storage.store(PAIR);

    expect(local.keys()).toEqual([REFRESH_TOKEN_KEY, REFRESH_TOKEN_EXPIRES_AT_KEY].sort());
    expect(local.getItem('battleship.refreshToken')).toBe('refresh-1');
    expect(local.getItem('battleship.refreshTokenExpiresAt')).toBe('2027-01-11T10:00:00Z');

    expect(session.keys()).toEqual([ACCESS_TOKEN_KEY, ACCESS_TOKEN_EXPIRES_AT_KEY].sort());
    expect(session.getItem('battleship.accessToken')).toBe('access-1');
    expect(session.getItem('battleship.accessTokenExpiresAt')).toBe('2026-10-13T10:15:00Z');
  });

  it('never writes the access token to localStorage', () => {
    storage.store(PAIR);

    const localValues = local.keys().map((k) => local.getItem(k));
    expect(localValues).not.toContain('access-1');
  });

  it('reads back what it stored', () => {
    storage.store(PAIR);

    expect(storage.read()).toEqual({
      refreshToken: { value: 'refresh-1', expiresAt: '2027-01-11T10:00:00Z' },
      accessToken: { value: 'access-1', expiresAt: '2026-10-13T10:15:00Z' },
    });
  });

  it('replaces the previous pair when a new one is stored', () => {
    storage.store(PAIR);
    storage.store({ ...PAIR, refreshToken: 'refresh-2', accessToken: 'access-2' });

    expect(storage.read().refreshToken?.value).toBe('refresh-2');
    expect(storage.read().accessToken?.value).toBe('access-2');
  });

  it('reads no tokens when nothing is stored', () => {
    expect(storage.read()).toEqual({ refreshToken: null, accessToken: null });
  });

  it('reads no token when its expiry is missing', () => {
    local.setItem(REFRESH_TOKEN_KEY, 'refresh-1');

    expect(storage.read().refreshToken).toBeNull();
  });

  it('removes all four keys on clear and leaves unrelated keys alone', () => {
    storage.store(PAIR);
    local.setItem('other', 'kept');

    storage.clear();

    expect(local.keys()).toEqual(['other']);
    expect(session.keys()).toEqual([]);
    expect(storage.read()).toEqual({ refreshToken: null, accessToken: null });
  });
});

describe('TokenStorage with blocked storage', () => {
  it('reads "no tokens" and never throws when accessing storage throws', () => {
    const blocked = (): Storage => {
      throw new DOMException('Access is denied', 'SecurityError');
    };
    const storage = setup(blocked, blocked);

    expect(() => storage.store(PAIR)).not.toThrow();
    expect(storage.read()).toEqual({ refreshToken: null, accessToken: null });
    expect(() => storage.clear()).not.toThrow();
  });

  it('reads "no tokens" when every storage call throws', () => {
    const throwing = new MemoryStorage();
    const fail = (): never => {
      throw new DOMException('Quota exceeded', 'QuotaExceededError');
    };
    throwing.getItem = fail;
    throwing.setItem = fail;
    throwing.removeItem = fail;
    const storage = setup(
      () => throwing,
      () => throwing,
    );

    expect(() => storage.store(PAIR)).not.toThrow();
    expect(storage.read()).toEqual({ refreshToken: null, accessToken: null });
    expect(() => storage.clear()).not.toThrow();
  });

  it('keeps the session token usable when only localStorage is blocked', () => {
    const session = new MemoryStorage();
    const storage = setup(
      () => {
        throw new DOMException('Access is denied', 'SecurityError');
      },
      () => session,
    );

    storage.store(PAIR);

    expect(storage.read()).toEqual({
      refreshToken: null,
      accessToken: { value: 'access-1', expiresAt: '2026-10-13T10:15:00Z' },
    });
  });
});
