import { TestBed } from '@angular/core/testing';
import { EventsService, eventsUrl } from './events.service';
import {
  EVENT_SOURCE_CLOSED,
  EVENT_SOURCE_CONNECTING,
  EVENT_SOURCE_FACTORY,
  EVENT_SOURCE_OPEN,
  EVENTS_API_URL,
  EventSourceLike,
} from './events.tokens';
import { RealtimeEvent } from './realtime-event';
import { HEARTBEAT_EVENT_TYPE } from './realtime-event-types';

const BASE_URL = 'http://events.test';

/** One HTTP request the fake made: the stream URL and the `Last-Event-ID` header it carried. */
interface FakeRequest {
  readonly url: string;
  readonly lastEventId: string | null;
}

/**
 * Behaves like the browser's `EventSource` as far as the service can tell: only listeners for a
 * named event receive it, the last seen id is remembered across events, and after a drop the fake
 * reconnects itself (on `reconnect()`) sending that id as `Last-Event-ID`, unless it was closed.
 */
class FakeEventSource implements EventSourceLike {
  readyState = EVENT_SOURCE_CONNECTING;
  readonly requests: FakeRequest[];
  #lastEventId = '';
  readonly #listeners = new Map<string, ((event: MessageEvent<string>) => void)[]>();

  constructor(readonly url: string) {
    this.requests = [{ url, lastEventId: null }];
  }

  addEventListener(type: string, listener: (event: MessageEvent<string>) => void): void {
    this.#listeners.set(type, [...(this.#listeners.get(type) ?? []), listener]);
  }

  close(): void {
    this.readyState = EVENT_SOURCE_CLOSED;
  }

  /** The server accepted the request. */
  open(): void {
    this.readyState = EVENT_SOURCE_OPEN;
    this.#dispatch('open', new MessageEvent('open'));
  }

  /** The server wrote one message; `id` is omitted for messages without an `id` field. */
  emit(type: string, data: string, id?: string): void {
    if (id !== undefined) this.#lastEventId = id;
    this.#dispatch(type, new MessageEvent(type, { data, lastEventId: this.#lastEventId }));
  }

  /** The connection dropped; the browser will retry. */
  drop(): void {
    this.readyState = EVENT_SOURCE_CONNECTING;
    this.#dispatch('error', new MessageEvent('error'));
  }

  /** The browser gave up (for example a non-200 response). */
  fail(): void {
    this.readyState = EVENT_SOURCE_CLOSED;
    this.#dispatch('error', new MessageEvent('error'));
  }

  /** The browser's retry timer fired. A closed source never retries. */
  reconnect(): void {
    if (this.readyState === EVENT_SOURCE_CLOSED) return;
    this.requests.push({ url: this.url, lastEventId: this.#lastEventId || null });
    this.open();
  }

  #dispatch(type: string, event: MessageEvent<string>): void {
    for (const listener of this.#listeners.get(type) ?? []) listener(event);
  }
}

function setup() {
  const sources: FakeEventSource[] = [];
  TestBed.configureTestingModule({
    providers: [
      { provide: EVENTS_API_URL, useValue: BASE_URL },
      {
        provide: EVENT_SOURCE_FACTORY,
        useValue: (url: string) => {
          const source = new FakeEventSource(url);
          sources.push(source);
          return source;
        },
      },
    ],
  });
  const service = TestBed.inject(EventsService);
  const received: RealtimeEvent[] = [];
  service.events$.subscribe((event) => received.push(event));
  return { service, sources, received };
}

describe('eventsUrl', () => {
  it('appends the events path and the encoded user id', () => {
    expect(eventsUrl('http://localhost:5317', 'A')).toBe('http://localhost:5317/events?userId=A');
    expect(eventsUrl('http://localhost:5317/', 'a b')).toBe(
      'http://localhost:5317/events?userId=a%20b',
    );
  });
});

describe('EventsService', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts disconnected without opening a stream', () => {
    const { service, sources } = setup();
    expect(service.state()).toBe('disconnected');
    expect(sources.length).toBe(0);
  });

  it('opens exactly one stream carrying the user id on connect', () => {
    const { service, sources } = setup();
    service.connect('A');
    expect(sources.length).toBe(1);
    expect(sources[0].url).toBe(`${BASE_URL}/events?userId=A`);
  });

  it('keeps the existing stream when connecting again for the same user', () => {
    const { service, sources } = setup();
    service.connect('A');
    sources[0].open();
    service.connect('A');
    expect(sources.length).toBe(1);
    expect(sources[0].readyState).toBe(EVENT_SOURCE_OPEN);
    expect(service.state()).toBe('open');
  });

  it('closes the current stream before connecting for a different user', () => {
    const { service, sources, received } = setup();
    service.connect('A');
    sources[0].open();
    service.connect('B');
    expect(sources.length).toBe(2);
    expect(sources[0].readyState).toBe(EVENT_SOURCE_CLOSED);
    expect(sources[1].url).toBe(`${BASE_URL}/events?userId=B`);
    // A late message on the old stream is ignored.
    sources[0].emit('game.shot-fired.v1', '{}', '1');
    expect(received).toEqual([]);
  });

  it('surfaces a named event with its type, id and parsed payload', () => {
    const { service, sources, received } = setup();
    service.connect('A');
    sources[0].open();
    sources[0].emit('game.shot-fired.v1', '{"x":3,"y":5}', '42');
    expect(received).toEqual([{ type: 'game.shot-fired.v1', id: '42', payload: { x: 3, y: 5 } }]);
  });

  it('surfaces a player ready event', () => {
    const { service, sources, received } = setup();
    service.connect('A');
    sources[0].open();
    sources[0].emit('game.player-ready.v1', '{"gameCode":"K7Q2M9XA"}', '7');
    expect(received).toEqual([
      { type: 'game.player-ready.v1', id: '7', payload: { gameCode: 'K7Q2M9XA' } },
    ]);
  });

  it('surfaces a turn changed event', () => {
    const { service, sources, received } = setup();
    service.connect('A');
    sources[0].open();
    sources[0].emit('game.turn-changed.v1', '{"gameCode":"K7Q2M9XA","playerWithTurnId":"B"}', '8');
    expect(received).toEqual([
      {
        type: 'game.turn-changed.v1',
        id: '8',
        payload: { gameCode: 'K7Q2M9XA', playerWithTurnId: 'B' },
      },
    ]);
  });

  it('does not surface heartbeats', () => {
    const { service, sources, received } = setup();
    service.connect('A');
    sources[0].open();
    sources[0].emit(HEARTBEAT_EVENT_TYPE, 'null');
    expect(received).toEqual([]);
  });

  it('skips and logs a malformed payload and keeps surfacing later events', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { service, sources, received } = setup();
    service.connect('A');
    sources[0].open();
    sources[0].emit('game.shot-fired.v1', '{not json', '1');
    expect(received).toEqual([]);
    expect(error).toHaveBeenCalledOnce();
    expect(sources[0].readyState).toBe(EVENT_SOURCE_OPEN);
    sources[0].emit('game.joined.v1', '{"player":"B"}', '2');
    expect(received).toEqual([{ type: 'game.joined.v1', id: '2', payload: { player: 'B' } }]);
  });

  it('logs messages without a registered event type', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    const { service, sources, received } = setup();
    service.connect('A');
    sources[0].open();
    sources[0].emit('message', '{"x":1}', '1');
    expect(received).toEqual([]);
    expect(warn).toHaveBeenCalledOnce();
  });

  it('follows the stream state from connecting to open to reconnecting', () => {
    const { service, sources } = setup();
    const states = [service.state()];
    service.connect('A');
    states.push(service.state());
    sources[0].open();
    states.push(service.state());
    sources[0].drop();
    states.push(service.state());
    expect(states).toEqual(['disconnected', 'connecting', 'open', 'reconnecting']);
  });

  it('reads disconnected when the browser gives up on the stream', () => {
    const { service, sources } = setup();
    service.connect('A');
    sources[0].fail();
    expect(service.state()).toBe('disconnected');
    // A later connect for the same user opens a fresh stream.
    service.connect('A');
    expect(sources.length).toBe(2);
  });

  it('reconnects with the last event id and surfaces the replayed events', () => {
    const { service, sources, received } = setup();
    service.connect('A');
    const source = sources[0];
    source.open();
    source.emit('game.shot-fired.v1', '{"x":1,"y":1}', '10');
    source.drop();
    expect(service.state()).toBe('reconnecting');

    source.reconnect();
    expect(service.state()).toBe('open');
    expect(source.requests.at(-1)).toEqual({
      url: `${BASE_URL}/events?userId=A`,
      lastEventId: '10',
    });
    source.emit('game.shot-fired.v1', '{"x":2,"y":2}', '11');
    source.emit('game.joined.v1', '{"player":"A"}', '12');

    expect(sources.length).toBe(1);
    expect(received.map((event) => event.id)).toEqual(['10', '11', '12']);
    expect(received[2]).toEqual({ type: 'game.joined.v1', id: '12', payload: { player: 'A' } });
  });

  it('closes the stream on disconnect and does not reconnect', () => {
    const { service, sources } = setup();
    service.connect('A');
    sources[0].open();
    service.disconnect();
    expect(sources[0].readyState).toBe(EVENT_SOURCE_CLOSED);
    expect(service.state()).toBe('disconnected');
    sources[0].reconnect();
    expect(sources[0].requests.length).toBe(1);
    expect(service.state()).toBe('disconnected');
  });

  it('closes the stream when the application is destroyed', () => {
    const { service, sources } = setup();
    let completed = false;
    service.events$.subscribe({ complete: () => (completed = true) });
    service.connect('A');
    sources[0].open();
    TestBed.resetTestingModule();
    expect(sources[0].readyState).toBe(EVENT_SOURCE_CLOSED);
    expect(completed).toBe(true);
  });
});
