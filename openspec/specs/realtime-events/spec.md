# realtime-events Specification

## Purpose

Defines how the server delivers integration events to connected browsers in real time: the shape of an event, the server-sent events stream clients subscribe to, which clients receive which events, and how the stream survives idle periods and reconnects.

## Requirements

### Requirement: Integration events share one envelope
Every event delivered to clients SHALL be an envelope with a unique id, an event type name, a target kind of `user`, `game` or `all`, a list of target user ids (empty for `all`), the instant the event occurred and a JSON payload. For a `game` event the target user ids are the user ids of the game's players. The Events service SHALL reject an envelope that lacks an id, a type or a valid target kind, or whose target kind is `user` or `game` with an empty target list. The former target kind `player` is not a valid target kind.

#### Scenario: Well-formed envelope is accepted
- **WHEN** an envelope with an id, a type, target kind `game`, two target user ids, an occurrence instant and a JSON payload arrives on the integration-events topic
- **THEN** the Events service accepts it and makes it available for delivery

#### Scenario: Envelope with missing target is rejected
- **WHEN** an envelope with target kind `user` and no target user ids arrives on the integration-events topic
- **THEN** the Events service rejects it, records a warning with the event id and type, and delivers nothing

#### Scenario: Former target kind is rejected
- **WHEN** an envelope with target kind `player` arrives on the integration-events topic
- **THEN** the Events service rejects it as having an invalid target kind and delivers nothing

### Requirement: Events published on the shared pub/sub component reach connected clients
The Events service SHALL subscribe to the `integration-events` topic on the shared `pubsub` component so that an envelope published there by any service is delivered to the targeted connections. On a local run an event SHALL reach a connected client within one second of publication.

#### Scenario: Published event arrives at a subscriber
- **WHEN** a service publishes a `user` envelope targeting user A on the `integration-events` topic while A holds an open connection
- **THEN** A's connection receives the event within one second

#### Scenario: Event with no matching connection is acknowledged
- **WHEN** an envelope targeting a user with no open connection is received from the topic
- **THEN** the Events service acknowledges the message so it is not redelivered, and no client receives it

### Requirement: Idle connections are kept alive
The Events service SHALL write a heartbeat message on every open connection at a fixed interval of at most thirty seconds while no event is delivered, so that proxies and browsers do not close an idle stream. Heartbeats SHALL be distinguishable from events and SHALL NOT carry an event id.

#### Scenario: Heartbeat during an idle period
- **WHEN** a connection receives no event for longer than the heartbeat interval
- **THEN** the client receives a heartbeat message before the interval elapses and the connection stays open

### Requirement: Disconnected clients release server resources
When a client closes its connection, the Events service SHALL stop writing to it, remove it from routing within the next heartbeat interval, and release any resources held for it. A delivery failure on one connection SHALL NOT affect delivery to other connections.

#### Scenario: Client disconnects
- **WHEN** a connected client closes the stream
- **THEN** the service removes the connection from routing and subsequent events targeting that user are not written to it

#### Scenario: One broken connection does not block others
- **WHEN** users A and B are connected, A's connection has failed without being detected yet, and an `all` event is received
- **THEN** B receives the event and A's failed connection is removed

### Requirement: Users subscribe through a server-sent events stream
The Events service SHALL expose a stream endpoint that, for a request identifying the connecting user with the `userId` query parameter, responds with content type `text/event-stream`, keeps the connection open until the client disconnects, and writes each delivered event as a server-sent events message with an `id` field set to a sequence number the service assigns to each routed event in increasing order (so a reconnecting client's last event id orders the replay), an `event` field set to the envelope type and a `data` field holding the envelope payload as JSON on a single line.

#### Scenario: Stream opens for an identified user
- **WHEN** a client requests the stream endpoint with `userId=A`
- **THEN** the response has content type `text/event-stream`, is not buffered, and stays open after the response headers are sent

#### Scenario: Event is written in server-sent events format
- **WHEN** an event with id `42`, type `shot-fired` and a JSON payload is delivered to a connected client
- **THEN** the client receives a message whose `id` is `42`, whose `event` is `shot-fired` and whose `data` is the payload JSON, terminated by a blank line

#### Scenario: Stream request without a user identity is refused
- **WHEN** a client requests the stream endpoint without a `userId` query parameter, including a request that only carries the former `playerId` parameter
- **THEN** the response is `400 Bad Request` with a problem-details body and no stream is opened

### Requirement: Events are routed to the users they target
The Events service SHALL deliver a `user` event only to connections of the listed users, a `game` event only to connections of the users listed in the event, and an `all` event to every open connection. A user with several open connections SHALL receive the event on each of them. A connection SHALL never receive an event that does not target it.

#### Scenario: User event reaches only that user
- **WHEN** users A and B each hold an open connection and a `user` event targeting A is received
- **THEN** A's connection receives the event and B's connection does not

#### Scenario: Game event reaches both participants
- **WHEN** users A, B and C each hold an open connection and a `game` event listing A and B is received
- **THEN** A and B receive the event and C does not

#### Scenario: Broadcast event reaches everyone
- **WHEN** users A and B each hold an open connection and an `all` event is received
- **THEN** both connections receive the event

#### Scenario: User with two tabs receives the event twice
- **WHEN** user A holds two open connections and a `user` event targeting A is received
- **THEN** both of A's connections receive the event

### Requirement: Reconnecting users receive missed events
The Events service SHALL keep the most recent delivered events in a bounded replay buffer. When a client reconnects and presents the id of the last event it received, the service SHALL deliver, in order, every buffered event that targets that user and was delivered after that id, before delivering new events. If the presented id is no longer in the buffer the service SHALL start from live events only.

#### Scenario: Missed events are replayed on reconnect
- **WHEN** user A's connection drops after receiving event `10`, events `11` and `12` targeting A are delivered while A is disconnected, and A reconnects presenting last event id `10`
- **THEN** A receives events `11` and `12` in order and then any newer events

#### Scenario: Replay does not leak other users' events
- **WHEN** user A reconnects presenting a last event id and the buffer holds events targeting only user B since that id
- **THEN** A receives none of B's events

#### Scenario: Last event id has fallen out of the buffer
- **WHEN** a client reconnects presenting an event id older than the oldest buffered event
- **THEN** the service opens the stream with live events only and does not fail the request
