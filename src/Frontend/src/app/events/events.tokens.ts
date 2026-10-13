import { InjectionToken } from '@angular/core';

/** The base URL of the Events API, without the `/events` path. Provided in `app.config.ts`. */
export const EVENTS_API_URL = new InjectionToken<string>('EVENTS_API_URL');

/** The `EventSource` readyState values, repeated here so a fake does not need the browser global. */
export const EVENT_SOURCE_CONNECTING = 0;
export const EVENT_SOURCE_OPEN = 1;
export const EVENT_SOURCE_CLOSED = 2;

/** The part of `EventSource` the events service uses, so tests can hand it a fake. */
export interface EventSourceLike {
  readonly readyState: number;
  addEventListener(type: string, listener: (event: MessageEvent<string>) => void): void;
  close(): void;
}

/** Opens an event stream for a URL. */
export type EventSourceFactory = (url: string) => EventSourceLike;

/** Creates the `EventSource` the events service listens to. Provided in `app.config.ts`. */
export const EVENT_SOURCE_FACTORY = new InjectionToken<EventSourceFactory>('EVENT_SOURCE_FACTORY');
