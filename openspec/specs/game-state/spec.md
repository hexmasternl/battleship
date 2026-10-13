# game-state Specification

## Purpose
Defines what a game is on the server: its identity and shareable code, its lifecycle states, its two players and the data held for each, the rule that governs whose turn it is, how activity is timestamped, and how the full game state is persisted.

## Requirements

### Requirement: A game has an id, a code and a state
Every game SHALL have:
- a technical id that is a GUID;
- a game code of exactly 8 characters, each one an uppercase letter `A`-`Z` or a digit `0`-`9`, unique among all stored games;
- a state that is one of `New`, `Started`, `Completed` or `Abandoned`.

#### Scenario: Code format
- **WHEN** a game is created
- **THEN** its code matches `^[A-Z0-9]{8}$` and its id is a non-empty GUID

#### Scenario: Codes are unique
- **WHEN** a newly generated code equals the code of a stored game
- **THEN** the new game is stored under a different code and the existing game is unchanged

### Requirement: A game has two player slots
A game SHALL have a player one and a player two. Each player SHALL carry:
- a player id;
- a player name;
- whether the player has the turn;
- whether the player is ready;
- the player's ship placements, each a ship kind, an origin cell and an orientation, at most one per kind;
- the shots the player has fired, each a column, a row and whether it hit;
- the instant of the player's last activity.

Player one SHALL be the player who created the game. Player two SHALL be empty until a second player joins.

#### Scenario: New game slots
- **WHEN** player A creates a game
- **THEN** player one is A with A's name, not ready, no ship placements and no shots, and player two is empty

### Requirement: Only one player has the turn
A game SHALL have at most one player whose has-turn flag is set. Only the game itself SHALL change the turn. Switching the turn SHALL clear the flag of the current player and set it for the other player in one operation. While a game is `New`, neither player SHALL have the turn.

#### Scenario: New game has no turn
- **WHEN** a game is created
- **THEN** neither player has the turn

#### Scenario: Turn switch flips both players
- **WHEN** player one has the turn in a started game and the turn is switched
- **THEN** player two has the turn and player one does not, with no observable moment where both or neither have it

#### Scenario: Turn cannot be given to both players
- **WHEN** an attempt is made to give the turn to a player while the other player already has it, without switching
- **THEN** the game refuses the change and still has exactly one player with the turn

### Requirement: Activity is timestamped
Every change to a game SHALL set the game's last activity instant to the current UTC time. A change made by or for a player SHALL also set that player's last activity instant.

#### Scenario: Creation sets activity
- **WHEN** a game is created at 10:00:00 UTC
- **THEN** the game's last activity and player one's last activity are 10:00:00 UTC

#### Scenario: Later change updates activity
- **WHEN** the turn of a game is switched at 10:05:00 UTC
- **THEN** the game's last activity is 10:05:00 UTC

### Requirement: The full game state is persisted as one JSON document
The Games service SHALL persist each game in table storage as one record keyed by game code. The record SHALL contain the complete game state as a serialized JSON object, plus the game id, state and last activity as separate columns. Reading a stored game SHALL restore exactly the state that was written.

#### Scenario: Round trip
- **WHEN** a game with player one, no player two and state `New` is stored and read back by its code
- **THEN** the restored game has the same id, code, state, players, coordinates and activity instants

#### Scenario: Local storage needs no setup
- **WHEN** the AppHost is started locally on a machine without Azure resources
- **THEN** the Games service stores games in the local storage emulator that the AppHost runs, and the table is created on first use

### Requirement: Stored games of an earlier document version are upgraded on read
The stored game document SHALL carry a document version. Reading a document of an earlier version SHALL upgrade it to the current shape before the game is used: a player without a ready flag SHALL be not ready, and shot coordinates without a hit flag SHALL become misses. Ship coordinates of an earlier version SHALL be dropped, because no earlier version could place ships. The next write SHALL store the current version.

#### Scenario: Version 1 game is read
- **WHEN** a game stored with document version 1, state `New` and player one A is read
- **THEN** the game has player one A, not ready, with no ship placements and no shots, and keeps its id, code, state and activity instants

#### Scenario: Upgraded game is written back
- **WHEN** a version 1 game is read, a ship is placed and the game is stored
- **THEN** the stored record has the current document version and holds the placement
