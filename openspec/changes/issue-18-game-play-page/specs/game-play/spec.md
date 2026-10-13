# Spec Delta

## Purpose

Defines how a started game is played on the server: who may fire a shot and where, how a shot is judged a hit or a miss and recorded, how the turn moves after every shot, and the integration events that announce shots and turn changes.

## ADDED Requirements

### Requirement: Firing a shot requires a player of the game
The Games API SHALL expose `POST /games/{code}/shots`, reachable from the frontend as `/api/games/{code}/shots` through the gateway, with a JSON body `{ column, row }`. It SHALL respond `401 Unauthorized` without a valid access token, and `404 Not Found` when the code is unknown or the caller is neither player one nor player two. The shooter SHALL be taken from the token subject.

#### Scenario: No token
- **WHEN** `POST /games/K7Q2M9XA/shots` is called without an `Authorization` header
- **THEN** the response is `401 Unauthorized` and the game is unchanged

#### Scenario: Caller is not a player
- **WHEN** player C, who is not a player of game `K7Q2M9XA`, fires a shot at that game
- **THEN** the response is `404 Not Found` and the game is unchanged

#### Scenario: Unknown code
- **WHEN** a shot is fired at a game code that is not stored
- **THEN** the response is `404 Not Found`

### Requirement: A shot must target a cell on the board
The column and the row of a shot SHALL each be an integer from 0 to 11. A request with a missing, non-integer or out-of-range column or row SHALL be refused with `400 Bad Request` problem details and SHALL NOT change the game or the turn.

#### Scenario: Outside the board
- **WHEN** the player with the turn fires at column 12, row 3
- **THEN** the response is `400 Bad Request`, no shot is recorded and the player keeps the turn

### Requirement: Only the player with the turn can fire in a started game
The Games API SHALL accept a shot only when the game is `Started` and the caller has the turn. Otherwise it SHALL respond `409 Conflict` with problem details whose stable `type` identifies the reason (game not started, or not the caller's turn), and SHALL NOT change the game.

#### Scenario: Not the caller's turn
- **WHEN** player B fires while player A has the turn
- **THEN** the response is `409 Conflict` with the not-your-turn problem type and no shot is recorded

#### Scenario: Game not started
- **WHEN** a player fires at a game that is still `New`
- **THEN** the response is `409 Conflict` with the game-not-started problem type and no shot is recorded

### Requirement: A player shoots at each cell at most once
A player SHALL NOT fire at a cell they have already fired at in the same game. Such a shot SHALL be refused with `409 Conflict` problem details whose stable `type` identifies a repeated cell, and the player SHALL keep the turn. Both players MAY fire at the same coordinates, because each fires at the other's board.

#### Scenario: Repeated cell
- **WHEN** player A, who has the turn and already fired at column 3, row 7, fires at column 3, row 7 again
- **THEN** the response is `409 Conflict` with the cell-already-shot problem type, no shot is recorded and A keeps the turn

### Requirement: A shot is judged and recorded
A shot SHALL be a hit when its cell is covered by one of the opponent's placed ships, and a miss otherwise. The shot SHALL be recorded on the shooter with its column, row and hit flag, and the response SHALL be `200 OK` with `{ column, row, hit }`. A recorded shot SHALL appear in the shooter's `me.shots` and in the opponent's `opponent.shots` when the game is read.

#### Scenario: Hit
- **WHEN** player A fires at column 4, row 2 and player B's destroyer covers that cell
- **THEN** the response is `200 OK` with `column` 4, `row` 2 and `hit` true, and A's recorded shots contain that cell as a hit

#### Scenario: Miss
- **WHEN** player A fires at a cell that none of player B's ships covers
- **THEN** the response has `hit` false and the shot is recorded as a miss

#### Scenario: Shot is visible to both players after a reload
- **WHEN** player A's shot at column 4, row 2 was a hit and player B reads the game
- **THEN** B's view lists the hit at column 4, row 2 under `opponent.shots`

### Requirement: The turn switches after every shot
After a recorded shot, hit or miss, the turn SHALL pass to the other player in the same stored update, and the game's and the shooter's last activity SHALL be set to the shot instant.

#### Scenario: Turn passes after a hit
- **WHEN** player A has the turn and fires a hit
- **THEN** player B has the turn and player A does not

#### Scenario: Turn passes after a miss
- **WHEN** player A has the turn and fires a miss
- **THEN** player B has the turn and player A does not

### Requirement: Concurrent shots fire at most once per turn
When two shot requests for the same game are processed at the same time, at most one SHALL be recorded for the turn. A request that loses a concurrent write SHALL be re-evaluated against the latest stored game and SHALL be refused when the caller no longer has the turn.

#### Scenario: Double submit
- **WHEN** player A, who has the turn, sends two shot requests for different cells at the same moment
- **THEN** exactly one shot is recorded, B has the turn, and the other request is answered `409 Conflict` with the not-your-turn problem type

### Requirement: Shots and turn changes are announced
After a shot is stored, the Games service SHALL publish to both players of the game, in this order, a `game.shot-fired.v1` event carrying the shooter, the cell and the hit flag, and then a `game.turn-changed.v1` event carrying the player who now has the turn. A failure to publish SHALL be logged with the game code and SHALL NOT undo the shot or fail the response.

#### Scenario: Events after a shot
- **WHEN** player A fires a miss at column 3, row 7 in game `K7Q2M9XA` with players A and B
- **THEN** a `game.shot-fired.v1` envelope with target kind `game`, targets A and B, and payload `playerId` A, `column` 3, `row` 7, `hit` false is published, followed by a `game.turn-changed.v1` envelope to A and B with `playerWithTurnId` B

#### Scenario: Publish failure
- **WHEN** the shot is stored but publishing fails
- **THEN** the response is still `200 OK` with the shot result, the shot and the turn switch remain stored, and an error naming the game code is logged
