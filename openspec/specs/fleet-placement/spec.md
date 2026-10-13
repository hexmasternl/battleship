# fleet-placement Specification

## Purpose

Defines the server side of the placement phase: the board and fleet rules, how a player places and moves ships, how a player becomes ready, and how a game starts once both players are ready.

## Requirements

### Requirement: The board is 12 by 12 and the fleet has five ships
The board SHALL have 12 columns and 12 rows, addressed by a 0-based `column` and `row` from 0 to 11. Each player's fleet SHALL be exactly one of each ship kind: `carrier` (5 cells), `battleship` (4), `cruiser` (3), `submarine` (3) and `destroyer` (2).

#### Scenario: Fleet composition
- **WHEN** the fleet rules are enumerated
- **THEN** they list five kinds with lengths carrier 5, battleship 4, cruiser 3, submarine 3 and destroyer 2, 17 cells in total

### Requirement: A ship placement covers cells from its origin
A ship placement SHALL consist of a kind, an origin cell and an orientation. A `horizontal` ship SHALL cover its length in consecutive columns starting at the origin column, on the origin row. A `vertical` ship SHALL cover its length in consecutive rows starting at the origin row, in the origin column.

#### Scenario: Horizontal cells
- **WHEN** a cruiser is placed at column 2, row 5, horizontal
- **THEN** it covers columns 2, 3 and 4 of row 5

#### Scenario: Vertical cells
- **WHEN** a destroyer is placed at column 7, row 10, vertical
- **THEN** it covers rows 10 and 11 of column 7

### Requirement: A player places or moves a ship
The Games API SHALL expose `PUT /games/{code}/ships/{kind}`, reachable as `/api/games/{code}/ships/{kind}` through the gateway, with a JSON body `{ column, row, orientation }`. It SHALL apply the same `401` and `404` rules as reading a game. When the placement is accepted, the caller's ship of that kind SHALL be placed, or moved when already placed, and the response SHALL be `200 OK` with the caller's view of the game.

#### Scenario: First placement
- **WHEN** player A, in a `New` game and not ready, places the carrier at column 0, row 0, horizontal
- **THEN** the response is `200 OK` and `me.ships` contains the carrier at column 0, row 0, horizontal

#### Scenario: Move and rotate
- **WHEN** player A's carrier is at column 0, row 0, horizontal and A places the carrier at column 0, row 0, vertical
- **THEN** the carrier is stored once, at column 0, row 0, vertical

#### Scenario: Kind is case-insensitive
- **WHEN** player A sends `PUT /games/K7Q2M9XA/ships/Carrier`
- **THEN** the request is handled as a placement of the carrier

#### Scenario: Not a player
- **WHEN** player C places a ship in a game whose players are A and B
- **THEN** the response is `404 Not Found` and the game is unchanged

### Requirement: A placement lies inside the board and does not overlap
A placement SHALL be refused with `400 Bad Request` problem details, and SHALL NOT change the game, when the kind is unknown, the orientation is not `horizontal` or `vertical`, the column or row is missing or not an integer, any covered cell lies outside the board, or any covered cell is covered by another ship of the same player. Ships MAY touch each other. The problem `type` SHALL distinguish a ship leaving the board from overlapping ships.

#### Scenario: Off the board
- **WHEN** player A places the battleship at column 9, row 0, horizontal
- **THEN** the response is `400 Bad Request` with the out-of-board problem type and the battleship is not placed

#### Scenario: Overlap
- **WHEN** player A's cruiser covers column 3, row 3 and A places the destroyer at column 3, row 2, vertical
- **THEN** the response is `400 Bad Request` with the overlap problem type and the destroyer is not placed

#### Scenario: Touching ships
- **WHEN** player A's cruiser covers columns 0 to 2 of row 0 and A places the destroyer at column 0, row 1, horizontal
- **THEN** the placement is accepted

#### Scenario: Moving onto its own cells
- **WHEN** player A's carrier covers columns 0 to 4 of row 0 and A places the carrier at column 1, row 0, horizontal
- **THEN** the placement is accepted, because a ship never overlaps itself

#### Scenario: Unknown kind
- **WHEN** player A sends `PUT /games/K7Q2M9XA/ships/rowboat`
- **THEN** the response is `400 Bad Request` and the game is unchanged

### Requirement: Ships can only be placed while placement is open for the caller
A placement SHALL be refused with `409 Conflict` problem details, and SHALL NOT change the game, when the game is not `New` or when the caller is already ready. The problem `type` SHALL distinguish the two reasons. A player SHALL be able to place ships before a second player has joined.

#### Scenario: Caller is ready
- **WHEN** player A is ready and places a ship
- **THEN** the response is `409 Conflict` with the player-ready problem type and A's ships are unchanged

#### Scenario: Game started
- **WHEN** a player places a ship in a `Started` game
- **THEN** the response is `409 Conflict` with the game-already-started problem type

#### Scenario: Alone in the game
- **WHEN** player A created a game nobody has joined and places the submarine
- **THEN** the placement is accepted

### Requirement: A player with a complete fleet becomes ready
The Games API SHALL expose `POST /games/{code}/ready`, reachable as `/api/games/{code}/ready` through the gateway, with no body and the same `401` and `404` rules as reading a game. When the game is `New` and all five of the caller's ships are placed, the caller SHALL become ready and the response SHALL be `200 OK` with the caller's view. A ready player SHALL stay ready; there is no way to become unready.

#### Scenario: Ready with a full fleet
- **WHEN** player A has placed all five ships and declares ready
- **THEN** the response is `200 OK` and `me.ready` is true

#### Scenario: Incomplete fleet
- **WHEN** player A has placed four ships and declares ready
- **THEN** the response is `409 Conflict` with the fleet-incomplete problem type and A is not ready

#### Scenario: Ready again
- **WHEN** player A is already ready and declares ready again
- **THEN** the response is `200 OK`, A stays ready, and no event is published

#### Scenario: Ready before the opponent joins
- **WHEN** player A is alone in the game with a full fleet and declares ready
- **THEN** A is ready and the game stays `New`

#### Scenario: Ready after the game ended its placement
- **WHEN** a player who is not ready declares ready in a game that is not `New`
- **THEN** the response is `409 Conflict` with the game-already-started problem type

### Requirement: The game starts when both players are ready
When a ready declaration leaves both player one and player two ready, the game SHALL become `Started` in the same stored update, and exactly one of the two players, chosen at random, SHALL get the turn. A game SHALL start at most once.

#### Scenario: Second player ready
- **WHEN** player A is ready and player B declares ready
- **THEN** the game is `Started` and exactly one of A and B has the turn

#### Scenario: First turn is random
- **WHEN** many games are started
- **THEN** the first turn goes to player one in some games and to player two in others

#### Scenario: Simultaneous ready
- **WHEN** players A and B, both with a full fleet, declare ready at the same moment
- **THEN** both requests answer `200 OK`, both players are ready, the game is `Started` with exactly one player holding the turn, and exactly one `game.started.v1` event is published

### Requirement: Readiness and the start are announced
After a player becomes ready, the Games service SHALL publish `game.player-ready.v1` with `gameId`, `gameCode` and `playerId` to the players of the game. When that update started the game, it SHALL then publish `game.started.v1` with `gameId`, `gameCode` and `playerWithTurnId` to both players. A failure to publish SHALL be logged with the game code and SHALL NOT undo the update or fail the response.

#### Scenario: Ready event
- **WHEN** player A becomes ready in game `K7Q2M9XA` that player B has joined
- **THEN** a `game.player-ready.v1` envelope with target kind `game`, targets A and B, and payload `playerId` A is published

#### Scenario: Ready while alone
- **WHEN** player A becomes ready before anyone joined
- **THEN** a `game.player-ready.v1` envelope targeting only A is published

#### Scenario: Start event
- **WHEN** player B's ready starts the game and player A gets the first turn
- **THEN** `game.player-ready.v1` for B is published, followed by `game.started.v1` to A and B with `playerWithTurnId` A

#### Scenario: Publish failure
- **WHEN** the ready update is stored but publishing fails
- **THEN** the response is still `200 OK`, the player stays ready, and an error naming the game code is logged
