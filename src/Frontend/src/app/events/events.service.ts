import { DestroyRef, inject, Service, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { EventsConnectionState, RealtimeEvent } from './realtime-event';
import { REALTIME_EVENT_TYPES, RealtimeEventType } from './realtime-event-types';
import {
  EVENT_SOURCE_CLOSED,
  EVENT_SOURCE_CONNECTING,
  EVENT_SOURCE_FACTORY,
  EVENTS_API_URL,
  EventSourceLike,
} from './events.tokens';

interface Connection {
  readonly userId: string;
  readonly source: EventSourceLike;
}

/** Builds the stream URL for a user: `<base>/events?userId=<id>`. */
export function eventsUrl(baseUrl: string, userId: string): string {
  return `${baseUrl.replace(/\/+$/, '')}/events?userId=${encodeURIComponent(userId)}`;
}

/**
 * The application-wide connection to the Events API stream. Holds at most one `EventSource`,
 * surfaces every received event on `events$` and the connection state on `state`. Reconnection and
 * `Last-Event-ID` replay are left to the browser's `EventSource`. The stream is closed on
 * `disconnect()` and when the root injector is destroyed.
 */
@Service()
export class EventsService {
  readonly #baseUrl = inject(EVENTS_API_URL);
  readonly #createSource = inject(EVENT_SOURCE_FACTORY);

  readonly #state = signal<EventsConnectionState>('disconnected');
  readonly #events = new Subject<RealtimeEvent>();
  #connection: Connection | null = null;

  /** The connection state of the stream. */
  readonly state = this.#state.asReadonly();

  /** Every received event, in the order received. Heartbeats are not included. */
  readonly events$: Observable<RealtimeEvent> = this.#events.asObservable();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.disconnect();
      this.#events.complete();
    });
  }

  /**
   * Opens the stream for a user. Does nothing while a stream for the same user is open or
   * reconnecting; a different user id closes the current stream first.
   */
  connect(userId: string): void {
    const current = this.#connection;
    if (current?.userId === userId && current.source.readyState !== EVENT_SOURCE_CLOSED) return;
    this.disconnect();

    const source = this.#createSource(eventsUrl(this.#baseUrl, userId));
    this.#connection = { userId, source };
    this.#state.set('connecting');

    const isCurrent = () => this.#connection?.source === source;
    source.addEventListener('open', () => {
      if (isCurrent()) this.#state.set('open');
    });
    source.addEventListener('error', () => {
      if (isCurrent()) this.#onError(source);
    });
    // Only the known types get a listener. `heartbeat` deliberately has none, so the browser drops
    // keep-alives before they reach the service.
    for (const type of REALTIME_EVENT_TYPES) {
      source.addEventListener(type, (event) => {
        if (isCurrent()) this.#onEvent(type, event);
      });
    }
    // Unnamed messages, or named ones the server sends as `message`, mean a publisher uses a type
    // that is missing from REALTIME_EVENT_TYPES. Log them so the gap is visible.
    source.addEventListener('message', (event) => {
      if (isCurrent()) console.warn('Events stream: unregistered message received', event.data);
    });
  }

  /** Closes the stream. The browser does not reconnect afterwards. */
  disconnect(): void {
    this.#connection?.source.close();
    this.#connection = null;
    this.#state.set('disconnected');
  }

  #onError(source: EventSourceLike): void {
    if (source.readyState === EVENT_SOURCE_CONNECTING) {
      this.#state.set('reconnecting');
    } else if (source.readyState === EVENT_SOURCE_CLOSED) {
      this.#connection = null;
      this.#state.set('disconnected');
    }
  }

  #onEvent(type: RealtimeEventType, event: MessageEvent<string>): void {
    let payload: unknown;
    try {
      payload = JSON.parse(event.data);
    } catch (error) {
      console.error(
        `Events stream: skipped '${type}' event ${event.lastEventId}, invalid JSON`,
        error,
      );
      return;
    }
    this.#events.next({ type, id: event.lastEventId, payload });
  }
}
