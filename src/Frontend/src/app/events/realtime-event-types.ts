/**
 * The event types the Events API pushes over the stream (the SSE `event` field, taken from the
 * integration event envelope's `type`).
 *
 * `EventSource` only dispatches a named event to a listener registered for that exact name, so
 * every type a publisher sends must be listed here or the browser silently drops it. This list is
 * kept in step by hand with the publishers that reference `HexMaster.Battleship.Events.Abstractions`;
 * adding a new event type is a change on both sides.
 */
export const REALTIME_EVENT_TYPES = [
  'shot-fired',
  'ships-placed',
  'game-started',
  'turn-changed',
] as const;

/** One of the event types the frontend listens for. */
export type RealtimeEventType = (typeof REALTIME_EVENT_TYPES)[number];

/** Keep-alive messages the server sends on an idle stream. They are never surfaced as events. */
export const HEARTBEAT_EVENT_TYPE = 'heartbeat';
