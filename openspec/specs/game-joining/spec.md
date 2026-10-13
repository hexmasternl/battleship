# game-joining Specification

## Purpose
Defines how a known player joins an existing game as player two: the request the frontend sends, how the caller is identified, when a join is refused, how a repeated join behaves, what the response contains, and the integration event that announces it.

## Requirements
### Requirement: Joining a game requires a known player
The Games API SHALL expose `POST /games/{game-code}/join`, reachable from the frontend as `/api/games/{game-code}/join` through the gateway. It SHALL accept a request only with a valid access token issued by the Players service, and SHALL respond `401 Unauthorized` without changing any game when the token is missing, expired or invalid.

#### Scenario: No token
- **WHEN** `POST /games/K7Q2M9XA/join` is called without an `Authorization` header
- **THEN** the response is `401 Unauthorized` and game `K7Q2M9XA` is unchanged

### Requirement: The joining player is taken from the token
The Games API SHALL take the joining player's id from the token subject and the player's name from the token name claim. The request SHALL have no body, and any player identity sent with it SHALL be ignored.

#### Scenario: Identity from the token
- **WHEN** player B with name `Admiral Tromp` joins game `K7Q2M9XA` with B's access token
- **THEN** player two of `K7Q2M9XA` has B's id and the name `Admiral Tromp`

### Requirement: An unknown game code is not found
When no stored game has the requested code, the Games API SHALL respond `404 Not Found` and change nothing. A code that does not match the game code format SHALL be treated as unknown. Codes SHALL be matched case-insensitively by upper-casing them.

#### Scenario: Unknown code
- **WHEN** a known player joins `ZZZZ9999` and no game has that code
- **THEN** the response is `404 Not Found`

#### Scenario: Lower-case code
- **WHEN** a known player joins `k7q2m9xa` and game `K7Q2M9XA` is `New` with an empty player two
- **THEN** the player joins game `K7Q2M9XA`

### Requirement: A free new game can be joined as player two
When the game is `New`, player two is empty and the caller is not player one, the Games API SHALL make the caller player two, keep the game `New` with neither player having the turn, set the last activity of the game and of player two to the join instant, and respond `200 OK` with a JSON body containing the game id and the game code.

#### Scenario: Successful join
- **WHEN** player B joins game `K7Q2M9XA`, created by player A and still waiting for a second player
- **THEN** the response is `200 OK` with `gameId` and `gameCode` `K7Q2M9XA`, player two is B, the game is still `New`, and neither player has the turn

### Requirement: A started game cannot be joined
When the game is not `New` and the caller is not one of its players, the Games API SHALL respond `409 Conflict` with a problem details body whose `type` identifies the game as already started, and SHALL change nothing.

#### Scenario: Game already started
- **WHEN** player C joins game `K7Q2M9XA`, which is `Started` with players A and B
- **THEN** the response is `409 Conflict` with the game-already-started problem type, and the game is unchanged

### Requirement: A full game cannot be joined
When the game is `New`, player two is another player and the caller is not one of its players, the Games API SHALL respond `409 Conflict` with a problem details body whose `type` identifies the game as full, distinct from the game-already-started type, and SHALL change nothing.

#### Scenario: Game full
- **WHEN** player C joins game `K7Q2M9XA`, which is `New` with players A and B
- **THEN** the response is `409 Conflict` with the game-full problem type, and player two is still B

### Requirement: Joining again is harmless
When the caller already is player one or player two of the game, in any state, the Games API SHALL respond `200 OK` with the game id and code, SHALL NOT change the game, and SHALL NOT publish an event.

#### Scenario: Player two joins again
- **WHEN** player B, already player two of game `K7Q2M9XA`, joins it again after refreshing the page
- **THEN** the response is `200 OK` with `gameCode` `K7Q2M9XA`, the game is unchanged and no event is published

#### Scenario: Creator enters the own code
- **WHEN** player A, player one of game `K7Q2M9XA`, joins it
- **THEN** the response is `200 OK`, player two stays empty and no event is published

### Requirement: Two simultaneous joins seat one player
When two players join the same free game at the same time, exactly one of them SHALL become player two. The other SHALL receive the game-full response. Neither write SHALL overwrite the other.

#### Scenario: Race for the second seat
- **WHEN** players B and C join free game `K7Q2M9XA` at the same moment
- **THEN** one of them receives `200 OK` and is player two, the other receives `409 Conflict` with the game-full problem type, and exactly one `game.joined.v1` event is published

### Requirement: Joining a game is announced
After a successful join is stored, the Games service SHALL publish a `game.joined.v1` integration event addressed to the game, targeting player one and player two, carrying the game id, the game code, and the joining player's id and name. A failure to publish SHALL be logged and SHALL NOT undo the join or fail the response.

#### Scenario: Event is published
- **WHEN** player B with name `Admiral Tromp` joins game `K7Q2M9XA` created by player A
- **THEN** a `game.joined.v1` envelope with target kind `game`, target player ids A and B, and payload `gameId`, `gameCode` `K7Q2M9XA`, `playerId` B and `playerName` `Admiral Tromp` is published on the integration-events topic

#### Scenario: Publish failure
- **WHEN** the join is stored but publishing the event fails
- **THEN** the response is still `200 OK`, player two remains B, and an error naming the game code is logged
