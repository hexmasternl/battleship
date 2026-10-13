/**
 * The event types the Events API pushes over the stream (the SSE `event` field, taken from the
 * integration event envelope's `type`).
 *
 * `EventSource` only dispatches a named event to a listener registered for that exact name, so
 * every type the backend publishes must be listed here or the browser silently drops it. The names
 * come from the integration event catalog in `HexMaster.Battleship.IntegrationEvents`
 * (`src/Shared/HexMaster.Battleship.IntegrationEvents`, see its README.md), which is the source of
 * truth for every versioned event type. A name added to or removed from that catalog must be
 * mirrored here.
 */
export const REALTIME_EVENT_TYPES = [
  'player.created.v1',
  'player.updated.v1',
  'game.created.v1',
  'game.joined.v1',
  'game.player-ready.v1',
  'game.started.v1',
  'game.shot-fired.v1',
  'game.turn-changed.v1',
  'game.ended.v1',
] as const;

/** One of the event types the frontend listens for. */
export type RealtimeEventType = (typeof REALTIME_EVENT_TYPES)[number];

/** Keep-alive messages the server sends on an idle stream. They are never surfaced as events. */
export const HEARTBEAT_EVENT_TYPE = 'heartbeat';
