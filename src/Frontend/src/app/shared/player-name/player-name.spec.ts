import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { axe } from 'vitest-axe';
import { MemoryStorage } from '../../../testing/memory-storage';
import { PlayerSession } from '../../core/session/player-session';
import {
  ACCESS_TOKEN_KEY,
  PERSISTENT_STORAGE,
  REFRESH_TOKEN_KEY,
  SESSION_STORAGE,
} from '../../core/session/token-storage';
import { PlayerName } from './player-name';
import {
  PLAYER_NAME_CREATE_FAILED_MESSAGE,
  PLAYER_NAME_REQUIRED_MESSAGE,
  PLAYER_NAME_TOO_LONG_MESSAGE,
} from './player-name.store';

const CREATE_URL = '/api/users/tokens';
const PAIR = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2999-01-01T00:00:00Z',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2999-01-01T00:00:00Z',
};

describe('PlayerName', () => {
  let fixture: ComponentFixture<PlayerName>;
  let host: HTMLElement;
  let backend: HttpTestingController;
  let local: MemoryStorage;
  let session: MemoryStorage;
  let created: number;

  const input = () => host.querySelector('input') as HTMLInputElement;
  const button = () => host.querySelector('button[type=submit]') as HTMLButtonElement;
  const settle = async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await fixture.whenStable();
  };

  async function type(value: string): Promise<void> {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function submit(): Promise<void> {
    button().click();
    await settle();
  }

  beforeEach(async () => {
    local = new MemoryStorage();
    session = new MemoryStorage();
    await TestBed.configureTestingModule({
      imports: [PlayerName],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PERSISTENT_STORAGE, useValue: () => local },
        { provide: SESSION_STORAGE, useValue: () => session },
      ],
    }).compileComponents();
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlayerName);
    created = 0;
    fixture.componentInstance.playerCreated.subscribe(() => created++);
    host = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => backend.verify());

  it('renders a visibly labelled name field and a submit button', () => {
    const label = host.querySelector('label') as HTMLLabelElement;
    expect(label.textContent?.trim()).toBe('Player name');
    expect(label.htmlFor).toBe(input().id);
    expect(button().textContent?.trim()).toBe('Take command');
    expect(button().disabled).toBe(false);
  });

  it.each([
    ['an empty name', '', PLAYER_NAME_REQUIRED_MESSAGE],
    ['a name of only spaces', '    ', PLAYER_NAME_REQUIRED_MESSAGE],
    ['a name longer than 40 characters', 'x'.repeat(41), PLAYER_NAME_TOO_LONG_MESSAGE],
  ])('shows a linked validation message and sends nothing for %s', async (_, value, message) => {
    await type(value);
    await submit();

    backend.expectNone(CREATE_URL);
    const hint = host.querySelector('.bs-field__hint--error') as HTMLElement;
    expect(hint.textContent?.trim()).toBe(message);
    expect(input().getAttribute('aria-invalid')).toBe('true');
    expect(input().getAttribute('aria-describedby')).toBe(hint.id);
    expect(created).toBe(0);
  });

  it('creates the player with the typed name, stores the tokens and signals its host', async () => {
    await type('Admiral Nelson');
    await submit();

    const req = backend.expectOne(CREATE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ displayName: 'Admiral Nelson' });
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(PAIR, { status: 201, statusText: 'Created' });
    await settle();

    expect(created).toBe(1);
    expect(local.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
    expect(session.getItem(ACCESS_TOKEN_KEY)).toBe('access-1');
    expect(TestBed.inject(PlayerSession).isKnown()).toBe(true);
  });

  it('disables the submit button while the request is pending and sends no second request', async () => {
    await type('Admiral Nelson');
    await submit();

    expect(button().disabled).toBe(true);
    host.querySelector('form')?.dispatchEvent(new Event('submit', { cancelable: true }));
    await settle();

    const req = backend.expectOne(CREATE_URL);
    req.flush(PAIR, { status: 201, statusText: 'Created' });
    await settle();
    expect(button().disabled).toBe(false);
    expect(created).toBe(1);
  });

  it('shows a retryable error, keeps the name and stores no tokens when creation fails', async () => {
    await type('Admiral Nelson');
    await submit();
    backend.expectOne(CREATE_URL).flush(null, { status: 503, statusText: 'Unavailable' });
    await settle();

    const alert = host.querySelector('[role=alert]') as HTMLElement;
    expect(alert.textContent?.trim()).toBe(PLAYER_NAME_CREATE_FAILED_MESSAGE);
    expect(input().value).toBe('Admiral Nelson');
    expect(button().disabled).toBe(false);
    expect(local.keys()).toEqual([]);
    expect(session.keys()).toEqual([]);
    expect(created).toBe(0);

    await submit();
    backend.expectOne(CREATE_URL).flush(PAIR, { status: 201, statusText: 'Created' });
    await settle();
    expect(host.querySelector('[role=alert]')).toBeNull();
    expect(created).toBe(1);
  });

  it('has no accessibility violations, also while showing errors', async () => {
    const summary = async () =>
      (await axe(host)).violations.map(
        (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
      );
    expect(await summary()).toEqual([]);

    await submit();
    expect(host.querySelector('.bs-field__hint--error')).not.toBeNull();
    expect(await summary()).toEqual([]);

    await type('Admiral Nelson');
    await submit();
    backend.expectOne(CREATE_URL).flush(null, { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(host.querySelector('[role=alert]')).not.toBeNull();
    expect(await summary()).toEqual([]);
  }, 20000);
});
