# Spec Delta

## Purpose

Defines how a player reads a game from the server: who may read it, and what the caller's view of the game contains and keeps hidden, so a page can be rebuilt from the server state at any moment.

## ADDED Requirements

### Requirement: Reading a game requires a player of the game
The Games API SHALL expose `GET /games/{code}`, reachable from the frontend as `/api/games/{code}` through the gateway. It SHALL respond `401 Unauthorized` without a valid access token. It SHALL respond `404 Not Found` with problem details when the code is unknown, when the code does not match the game code format, or when the caller (the token subject) is neither player one nor player two of the game. The code SHALL be matched case-insensitively.

#### Scenario: No token
- **WHEN** `GET /games/K7Q2M9XA` is called without an `Authorization` header
- **THEN** the response is `401 Unauthorized`

#### Scenario: Unknown code
- **WHEN** player A reads a game code that is not stored
- **THEN** the response is `404 Not Found`

#### Scenario: Caller is not a player
- **WHEN** player C reads game `K7Q2M9XA` whose players are A and B
- **THEN** the response is `404 Not Found`, the same response as for an unknown code

#### Scenario: Lower-case code
- **WHEN** player A reads `/games/k7q2m9xa` and A is a player of game `K7Q2M9XA`
- **THEN** the response is `200 OK` with the view of game `K7Q2M9XA`

### Requirement: The view describes the game from the caller's side
A successful read SHALL return `200 OK` with a JSON object holding `gameId`, `gameCode`, `state`, `me` and `opponent`. `me` SHALL describe the caller: `playerId`, `playerName`, `ready`, `hasTurn`, `ships` and `shots`. `opponent` SHALL be `null` while the game has no second player, and otherwise describe the other player with `playerId`, `playerName`, `ready`, `hasTurn` and `shots`.

#### Scenario: Creator before anyone joined
- **WHEN** player A created game `K7Q2M9XA` and reads it before anyone joined
- **THEN** `state` is `New`, `me.playerId` is A, `me.ready` and `me.hasTurn` are false, `me.ships` and `me.shots` are empty, and `opponent` is `null`

#### Scenario: Player two reads the game
- **WHEN** player B joined game `K7Q2M9XA` created by A and B reads it
- **THEN** `me` describes B and `opponent` describes A with A's name and ready flag

### Requirement: The view shows the caller's ships and never the opponent's
`me.ships` SHALL list each ship the caller has placed as `{ kind, column, row, orientation }`, where `kind` is one of `carrier`, `battleship`, `cruiser`, `submarine` or `destroyer`, `column` and `row` are the 0-based origin cell, and `orientation` is `horizontal` or `vertical`. The view SHALL NOT contain the opponent's ships, their cells or their count, in any game state.

#### Scenario: Own ships are returned
- **WHEN** player A placed the carrier at column 0, row 0, horizontal and reads the game
- **THEN** `me.ships` contains `{ kind: "carrier", column: 0, row: 0, orientation: "horizontal" }`

#### Scenario: Opponent ships are hidden
- **WHEN** player B placed all five ships and player A reads the game
- **THEN** the response body contains no ship placement or ship cell of player B

### Requirement: The view shows all shots with their result
`me.shots` SHALL list the shots the caller fired and `opponent.shots` the shots the opponent fired, each as `{ column, row, hit }`, in the order they were fired.

#### Scenario: No shots during placement
- **WHEN** a game is `New` and either player reads it
- **THEN** `me.shots` is empty and `opponent.shots`, when an opponent exists, is empty

#### Scenario: Recorded shot is listed
- **WHEN** the caller fired a shot at column 3, row 7 that was recorded as a miss
- **THEN** `me.shots` contains `{ column: 3, row: 7, hit: false }`

### Requirement: Reading a game changes nothing
Reading a game SHALL NOT change the game, its last activity or any player's last activity, and SHALL NOT publish an event.

#### Scenario: Read is side-effect free
- **WHEN** player A reads game `K7Q2M9XA` twice
- **THEN** both responses are equal and the stored game's last activity is unchanged
