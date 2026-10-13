# realtime-events-client Specification

## Purpose

Defines how the frontend application consumes the server's real-time event stream: opening it for the current player, exposing the events and the connection state to the rest of the application, recovering from dropped connections and releasing the stream on shutdown.

## Requirements

### Requirement: The application opens one event stream for the current player
The frontend SHALL provide a single application-wide events service that opens the server's event stream for the current player identity on request and keeps at most one stream open per application instance. Opening the stream again while it is open SHALL NOT create a second connection.

#### Scenario: First connect opens the stream
- **WHEN** the application asks the events service to connect for player A
- **THEN** exactly one stream to the server's events endpoint is opened carrying A's identity

#### Scenario: Repeated connect is idempotent
- **WHEN** the stream is already open and the application asks the events service to connect again for the same player
- **THEN** no second stream is opened and the existing one stays in use

### Requirement: Received events are surfaced with their type and payload
The events service SHALL expose every received event to the application as a value with the event type, the event id and the parsed JSON payload, in the order received. Heartbeat messages SHALL NOT be surfaced as events. A message whose payload is not valid JSON SHALL be reported as an error and skipped without closing the stream.

#### Scenario: Event is surfaced to a subscriber
- **WHEN** the stream delivers a message with event type `shot-fired`, id `42` and a JSON payload
- **THEN** every subscriber of the events service receives one value with type `shot-fired`, id `42` and the parsed payload

#### Scenario: Malformed payload is skipped
- **WHEN** the stream delivers a message whose data is not valid JSON
- **THEN** no event value is emitted, an error is logged, and later messages are still surfaced

### Requirement: Connection state is observable
The events service SHALL expose the connection state as a reactive value with at least the states `disconnected`, `connecting`, `open` and `reconnecting`, updated whenever the underlying stream changes state, so the application can show whether live updates are available.

#### Scenario: State follows the stream lifecycle
- **WHEN** the application connects, the stream opens, the stream later drops and the browser starts reconnecting
- **THEN** the connection state reads `connecting`, then `open`, then `reconnecting`, in that order

### Requirement: Dropped connections are recovered without losing events
After a dropped stream the events service SHALL let the stream reconnect automatically and present the id of the last received event so the server can replay missed events. Events received during replay SHALL be surfaced exactly like live events.

#### Scenario: Reconnect presents the last event id
- **WHEN** the stream drops after event `10` was received and the stream reconnects
- **THEN** the reconnect request carries last event id `10` and the replayed events `11` and `12` are surfaced to subscribers

### Requirement: The stream is closed when no longer needed
The events service SHALL close the stream when the application explicitly disconnects and when the application is destroyed, and SHALL set the connection state to `disconnected`. After an explicit disconnect the browser SHALL NOT reconnect on its own.

#### Scenario: Explicit disconnect
- **WHEN** the application asks the events service to disconnect
- **THEN** the stream is closed, the state reads `disconnected` and no reconnection attempt is made

#### Scenario: Application teardown
- **WHEN** the application instance is destroyed while the stream is open
- **THEN** the stream is closed
