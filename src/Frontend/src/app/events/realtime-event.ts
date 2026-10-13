import { RealtimeEventType } from './realtime-event-types';

/** One event received from the Events API stream. */
export interface RealtimeEvent {
  /** The event type, the SSE `event` field. */
  readonly type: RealtimeEventType;
  /** The server-assigned sequence id, the SSE `id` field. */
  readonly id: string;
  /** The parsed JSON `data` field. Its shape depends on `type`. */
  readonly payload: unknown;
}

/** The connection state of the event stream. */
export type EventsConnectionState = 'disconnected' | 'connecting' | 'open' | 'reconnecting';
