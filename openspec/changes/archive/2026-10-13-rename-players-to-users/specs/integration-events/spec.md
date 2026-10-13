# Spec Delta

## MODIFIED Requirements

### Requirement: The system publishes a fixed catalog of integration events
The backend SHALL define exactly one catalog of integration events. Every event a backend service publishes SHALL be a member of it. The catalog SHALL contain at least: user created, user updated (display name changed), game created, game joined (second player joined), player ready (a player finished placing ships), game started, shot fired, turn changed and game ended (by a player or by the server). User events belong to the Users domain; game events refer to the game's players, whose ids are user ids.

#### Scenario: Catalog lists the known events
- **WHEN** the catalog is enumerated
- **THEN** it contains the types `user.created.v1`, `user.updated.v1`, `game.created.v1`, `game.joined.v1`, `game.player-ready.v1`, `game.started.v1`, `game.shot-fired.v1`, `game.turn-changed.v1` and `game.ended.v1`, and no type starting with `player.`

#### Scenario: Event outside the catalog cannot be published
- **WHEN** a service tries to publish an object that is not a catalog event
- **THEN** the publish is rejected before anything reaches the pub/sub component

### Requirement: Breaking payload changes add a new version
A published version of an event SHALL NOT change incompatibly. Removing or renaming a field, or changing its meaning or type, SHALL be done by adding the same event with the next version number next to the existing one, which stays in the catalog until no publisher uses it. Adding an optional field SHALL NOT require a new version.

#### Scenario: Two versions coexist
- **WHEN** a breaking change to the user created payload is introduced
- **THEN** the catalog contains both `user.created.v1` and `user.created.v2`, and envelopes of either type can be published

### Requirement: Each event carries a minimal payload
Each catalog event SHALL carry the identifiers needed to act on it and no more, serialized as camelCase JSON:
- user created and user updated: `userId`, `displayName`
- game created: `gameId`, `gameCode`, `playerId` (the creator)
- game joined: `gameId`, `gameCode`, `playerId`, `playerName`
- player ready: `gameId`, `gameCode`, `playerId`
- game started: `gameId`, `gameCode`, `playerWithTurnId`
- shot fired: `gameId`, `gameCode`, `playerId` (the shooter), `column`, `row`, `hit`
- turn changed: `gameId`, `gameCode`, `playerWithTurnId`
- game ended: `gameId`, `gameCode`, `reason`, optional `winnerId`

Every `playerId`, `playerWithTurnId` and `winnerId` in a game event SHALL be the user id of that player.

#### Scenario: User created payload
- **WHEN** a user created event is published for user A with display name `Admiral Nelson`
- **THEN** the envelope payload is a JSON object with `userId` A and `displayName` `Admiral Nelson`

#### Scenario: Shot fired payload
- **WHEN** player A fires at column 3, row 7 in game `ABCD1234` and hits a ship
- **THEN** the envelope payload is a JSON object with `gameId`, `gameCode` `ABCD1234`, `playerId` A's user id, `column` 3, `row` 7 and `hit` true

#### Scenario: Turn changed payload
- **WHEN** the turn in game `ABCD1234` passes to player B
- **THEN** the envelope has type `game.turn-changed.v1` and its payload is a JSON object with `gameId`, `gameCode` `ABCD1234` and `playerWithTurnId` B's user id

#### Scenario: Player ready payload
- **WHEN** player A declares ready in game `ABCD1234`
- **THEN** the envelope has type `game.player-ready.v1` and its payload is a JSON object with `gameId`, `gameCode` `ABCD1234` and `playerId` A's user id

#### Scenario: Game ended by the server
- **WHEN** the server ends an inactive game
- **THEN** the game ended payload carries a `reason` that identifies the server as the cause and no `winnerId`

### Requirement: Events are published in the shared envelope on the integration-events topic
A backend service SHALL publish a catalog event by wrapping it in the integration event envelope and publishing that envelope to the `integration-events` topic on the shared `pubsub` component. The envelope SHALL have:
- a new unique id;
- the event's type name;
- the target kind and target user ids the publisher specifies;
- the publication instant in UTC;
- the event as its payload.

#### Scenario: Envelope reaches the Events service
- **WHEN** the Users service publishes a user created event targeting user A while A holds an open event stream
- **THEN** the Events service accepts the envelope and A's stream receives an event of type `user.created.v1` with the payload

#### Scenario: Each publish gets its own id
- **WHEN** the same event is published twice
- **THEN** the two envelopes have different ids

## REMOVED Requirements

### Requirement: The publisher states who an event is for
**Reason**: Audiences address users, not players; the scenario names change with it.
**Migration**: Replaced by "The publisher states which users an event is for". `EventAudience.Players(...)` becomes `EventAudience.Users(...)`.

## ADDED Requirements

### Requirement: The publisher states which users an event is for
Every publish SHALL state the audience: specific users, the players of a game (given by their user ids), or everyone. A publish addressed to specific users or to a game with no user id SHALL be rejected before anything reaches the pub/sub component.

#### Scenario: Game event addressed to both players
- **WHEN** a game joined event is published for a game whose players are users A and B
- **THEN** the envelope has target kind `game` and target user ids A and B

#### Scenario: Event addressed to one user
- **WHEN** a game created event is published for its creator, user A
- **THEN** the envelope has target kind `user` and target user ids A

#### Scenario: User audience without users is rejected
- **WHEN** a service publishes an event addressed to specific users with an empty user list
- **THEN** the publish fails with an error and no envelope is published
