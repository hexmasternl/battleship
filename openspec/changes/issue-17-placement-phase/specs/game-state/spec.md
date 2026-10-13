# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Stored games of an earlier document version are upgraded on read
The stored game document SHALL carry a document version. Reading a document of an earlier version SHALL upgrade it to the current shape before the game is used: a player without a ready flag SHALL be not ready, and shot coordinates without a hit flag SHALL become misses. Ship coordinates of an earlier version SHALL be dropped, because no earlier version could place ships. The next write SHALL store the current version.

#### Scenario: Version 1 game is read
- **WHEN** a game stored with document version 1, state `New` and player one A is read
- **THEN** the game has player one A, not ready, with no ship placements and no shots, and keeps its id, code, state and activity instants

#### Scenario: Upgraded game is written back
- **WHEN** a version 1 game is read, a ship is placed and the game is stored
- **THEN** the stored record has the current document version and holds the placement
