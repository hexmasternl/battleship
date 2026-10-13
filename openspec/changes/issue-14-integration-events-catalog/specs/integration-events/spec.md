# Spec Delta

## Purpose

Defines the integration events the backend services publish: which events exist, how each is named and versioned, what each payload carries, who each is addressed to, and how a service publishes one onto the shared integration-events topic.

## ADDED Requirements

### Requirement: The system publishes a fixed catalog of integration events
The backend SHALL define exactly one catalog of integration events. Every event a backend service publishes SHALL be a member of it. The catalog SHALL contain at least: player created, player updated (name changed), game created, game joined (second player joined), game started, shot fired and game ended (by a player or by the server).

#### Scenario: Catalog lists the known events
- **WHEN** the catalog is enumerated
- **THEN** it contains the types `player.created.v1`, `player.updated.v1`, `game.created.v1`, `game.joined.v1`, `game.started.v1`, `game.shot-fired.v1` and `game.ended.v1`

#### Scenario: Event outside the catalog cannot be published
- **WHEN** a service tries to publish an object that is not a catalog event
- **THEN** the publish is rejected before anything reaches the pub/sub component

### Requirement: Every event has a unique versioned type name
Each catalog event SHALL have a type name of the form `<subject>.<action>.v<version>`, in lowercase with kebab-case segments and a positive integer version. Type names SHALL be unique within the catalog. The type name SHALL be the envelope `type`, and therefore the event name clients receive.

#### Scenario: Type names follow the format
- **WHEN** the type names of all catalog events are inspected
- **THEN** each matches `<subject>.<action>.v<version>` and no two are equal

#### Scenario: Published envelope carries the type name
- **WHEN** a game started event is published
- **THEN** the envelope on the topic has type `game.started.v1`

### Requirement: Breaking payload changes add a new version
A published version of an event SHALL NOT change incompatibly. Removing or renaming a field, or changing its meaning or type, SHALL be done by adding the same event with the next version number next to the existing one, which stays in the catalog until no publisher uses it. Adding an optional field SHALL NOT require a new version.

#### Scenario: Two versions coexist
- **WHEN** a breaking change to the player created payload is introduced
- **THEN** the catalog contains both `player.created.v1` and `player.created.v2`, and envelopes of either type can be published

### Requirement: Each event carries a minimal payload
Each catalog event SHALL carry the identifiers needed to act on it and no more, serialized as camelCase JSON:
- player created and player updated: `playerId`, `playerName`
- game created: `gameId`, `gameCode`, `playerId` (the creator)
- game joined: `gameId`, `gameCode`, `playerId`, `playerName`
- game started: `gameId`, `gameCode`, `playerWithTurnId`
- shot fired: `gameId`, `gameCode`, `playerId`, `column`, `row`
- game ended: `gameId`, `gameCode`, `reason`, optional `winnerId`

#### Scenario: Shot fired payload
- **WHEN** player A fires at column 3, row 7 in game `ABCD1234`
- **THEN** the envelope payload is a JSON object with `gameId`, `gameCode` `ABCD1234`, `playerId` A, `column` 3 and `row` 7

#### Scenario: Game ended by the server
- **WHEN** the server ends an inactive game
- **THEN** the game ended payload carries a `reason` that identifies the server as the cause and no `winnerId`

### Requirement: Events are published in the shared envelope on the integration-events topic
A backend service SHALL publish a catalog event by wrapping it in the integration event envelope and publishing that envelope to the `integration-events` topic on the shared `pubsub` component. The envelope SHALL have:
- a new unique id;
- the event's type name;
- the target kind and target player ids the publisher specifies;
- the publication instant in UTC;
- the event as its payload.

#### Scenario: Envelope reaches the Events service
- **WHEN** the Players service publishes a player created event targeting player A while A holds an open event stream
- **THEN** the Events service accepts the envelope and A's stream receives an event of type `player.created.v1` with the payload

#### Scenario: Each publish gets its own id
- **WHEN** the same event is published twice
- **THEN** the two envelopes have different ids

### Requirement: The publisher states who an event is for
Every publish SHALL state the audience: specific players, the players of a game, or everyone. A publish addressed to specific players or to a game with no player id SHALL be rejected before anything reaches the pub/sub component.

#### Scenario: Game event addressed to both players
- **WHEN** a game joined event is published for a game with players A and B
- **THEN** the envelope has target kind `game` and target player ids A and B

#### Scenario: Player audience without players is rejected
- **WHEN** a service publishes an event addressed to specific players with an empty player list
- **THEN** the publish fails with an error and no envelope is published

### Requirement: Every backend service can publish without its own wiring
Every backend service that applies the shared service defaults SHALL be able to publish catalog events with no further registration or configuration. The pub/sub component name and topic name SHALL come from one shared definition, the same one the application host uses to declare the component.

#### Scenario: Service publishes after applying the defaults
- **WHEN** a backend service applies the shared service defaults and requests the event sender
- **THEN** a sender is available that publishes to `integration-events` on `pubsub`

#### Scenario: Component name has one source
- **WHEN** the pub/sub component name used by the application host, by the sender and by the Events service subscription is compared
- **THEN** all three come from the same shared constant
