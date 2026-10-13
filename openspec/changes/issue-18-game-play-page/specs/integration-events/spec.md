# Spec Delta

## MODIFIED Requirements

### Requirement: The system publishes a fixed catalog of integration events
The backend SHALL define exactly one catalog of integration events. Every event a backend service publishes SHALL be a member of it. The catalog SHALL contain at least: player created, player updated (name changed), game created, game joined (second player joined), player ready (a player finished placing ships), game started, shot fired, turn changed and game ended (by a player or by the server).

#### Scenario: Catalog lists the known events
- **WHEN** the catalog is enumerated
- **THEN** it contains the types `player.created.v1`, `player.updated.v1`, `game.created.v1`, `game.joined.v1`, `game.player-ready.v1`, `game.started.v1`, `game.shot-fired.v1`, `game.turn-changed.v1` and `game.ended.v1`

#### Scenario: Event outside the catalog cannot be published
- **WHEN** a service tries to publish an object that is not a catalog event
- **THEN** the publish is rejected before anything reaches the pub/sub component

### Requirement: Each event carries a minimal payload
Each catalog event SHALL carry the identifiers needed to act on it and no more, serialized as camelCase JSON:
- player created and player updated: `playerId`, `playerName`
- game created: `gameId`, `gameCode`, `playerId` (the creator)
- game joined: `gameId`, `gameCode`, `playerId`, `playerName`
- player ready: `gameId`, `gameCode`, `playerId`
- game started: `gameId`, `gameCode`, `playerWithTurnId`
- shot fired: `gameId`, `gameCode`, `playerId` (the shooter), `column`, `row`, `hit`
- turn changed: `gameId`, `gameCode`, `playerWithTurnId`
- game ended: `gameId`, `gameCode`, `reason`, optional `winnerId`

#### Scenario: Shot fired payload
- **WHEN** player A fires at column 3, row 7 in game `ABCD1234` and hits a ship
- **THEN** the envelope payload is a JSON object with `gameId`, `gameCode` `ABCD1234`, `playerId` A, `column` 3, `row` 7 and `hit` true

#### Scenario: Turn changed payload
- **WHEN** the turn in game `ABCD1234` passes to player B
- **THEN** the envelope has type `game.turn-changed.v1` and its payload is a JSON object with `gameId`, `gameCode` `ABCD1234` and `playerWithTurnId` B

#### Scenario: Player ready payload
- **WHEN** player A declares ready in game `ABCD1234`
- **THEN** the envelope has type `game.player-ready.v1` and its payload is a JSON object with `gameId`, `gameCode` `ABCD1234` and `playerId` A

#### Scenario: Game ended by the server
- **WHEN** the server ends an inactive game
- **THEN** the game ended payload carries a `reason` that identifies the server as the cause and no `winnerId`
