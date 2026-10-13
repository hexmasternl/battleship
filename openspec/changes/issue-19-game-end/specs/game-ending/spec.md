# Spec Delta

## Purpose

Defines how the server finishes a started game: detecting after each shot that a player's whole fleet is sunk, marking the game completed with its winner, announcing the end to both players, and keeping a finished game unchanged afterwards.

## ADDED Requirements

### Requirement: The server checks for the end of the game after every shot
After the Games service records a shot in a `Started` game, it SHALL check whether every cell of every ship of the opponent has been hit by the shooter. When at least one opponent ship cell is not yet hit, the game SHALL continue as before.

#### Scenario: Last ship cell is hit
- **WHEN** player A's shots have hit all 17 cells of player B's fleet except one, and A fires at that last cell
- **THEN** the shot is recorded as a hit and the game ends with A as the winner

#### Scenario: Fleet not yet sunk
- **WHEN** player A hits a cell of player B's fleet while another cell of B's fleet is still not hit
- **THEN** the game stays `Started`, has no winner, and the turn switches to B

#### Scenario: Miss never ends the game
- **WHEN** player A misses while one cell of B's fleet is still not hit
- **THEN** the game stays `Started` and the turn switches to B

### Requirement: A completed game has a winner and no turn
When the shot hits the opponent's last unhit ship cell, the game SHALL become `Completed`, its winner SHALL be the shooter, and neither player SHALL have the turn. This SHALL take precedence over the rule that the turn switches after every shot: the winning shot SHALL NOT switch the turn. The game's and the shooter's last activity SHALL be the instant of the winning shot.

#### Scenario: Winning shot completes the game
- **WHEN** player A's shot at 10:20:00 UTC hits the last unhit cell of B's fleet
- **THEN** the game state is `Completed`, the winner is A, neither A nor B has the turn, and the game's and A's last activity are 10:20:00 UTC

#### Scenario: Running games have no winner
- **WHEN** a game is `New` or `Started`
- **THEN** it has no winner

### Requirement: The winner is persisted with the game
The persisted game state SHALL include the winner. Reading a completed game back SHALL restore its `Completed` state and its winner. A game stored before the winner existed SHALL read back without a winner and otherwise unchanged.

#### Scenario: Completed game round trip
- **WHEN** a `Completed` game won by player A is stored and read back by its code
- **THEN** the restored game is `Completed`, its winner is A and neither player has the turn

#### Scenario: Older game without a winner
- **WHEN** a game stored before this change is read
- **THEN** it is restored with no winner and its other state is unchanged

### Requirement: The end of the game is announced to both players
When a shot completes the game, the Games service SHALL publish the shot fired event for that shot first, then a `game.ended.v1` event addressed to both players of the game with reason `completed` and the winner's player id. It SHALL NOT publish a turn changed event for the winning shot. A publish failure SHALL be logged with the game code and SHALL NOT undo the completed game or fail the shot.

#### Scenario: Events on the winning shot
- **WHEN** player A's shot completes game `ABCD1234` with players A and B
- **THEN** a `game.shot-fired.v1` envelope for the shot is published, followed by a `game.ended.v1` envelope with target kind `game`, target player ids A and B, and payload `gameId`, `gameCode` `ABCD1234`, `reason` `completed` and `winnerId` A

#### Scenario: No turn change on the winning shot
- **WHEN** player A's shot completes the game
- **THEN** no `game.turn-changed.v1` envelope is published for that shot

#### Scenario: Publish failure after the winning shot
- **WHEN** the winning shot is stored but publishing `game.ended.v1` fails
- **THEN** the shot response still succeeds, the game remains `Completed` with A as the winner, and an error naming the game code is logged

### Requirement: A completed game accepts no more shots
`POST /games/{code}/shots` on a `Completed` game SHALL respond `409 Conflict` with problem details, for either player. The game SHALL stay unchanged and no event SHALL be published.

#### Scenario: Shot after the end
- **WHEN** player B fires at a cell of game `ABCD1234` after A has won it
- **THEN** the response is `409 Conflict`, no shot is recorded, the game is unchanged and no event is published

#### Scenario: Winner fires again
- **WHEN** player A, who won the game, fires again
- **THEN** the response is `409 Conflict` and the game is unchanged

### Requirement: The game view reports the winner
The response of `GET /games/{code}` SHALL include `winnerId`: the winning player's id when the game is `Completed` with a winner, and `null` otherwise. Every other rule of the game view SHALL stay as it is; in particular, the opponent's ships SHALL NOT be revealed after the game ends.

#### Scenario: View of a completed game
- **WHEN** player B requests the view of a game that player A has won
- **THEN** the response has `state` `Completed`, `winnerId` A, neither player with the turn, and no ships of A

#### Scenario: View of a running game
- **WHEN** a player requests the view of a `Started` game
- **THEN** the response has `winnerId` `null`

### Requirement: The shot response reports the game state
The response of a successful `POST /games/{code}/shots` SHALL include, next to the shot's `column`, `row` and `hit`, the `state` of the game after the shot, so the shooter learns from the response alone that the shot ended the game.

#### Scenario: Winning shot response
- **WHEN** player A's shot at column 4, row 9 completes the game
- **THEN** the response has `column` 4, `row` 9, `hit` `true` and `state` `Completed`

#### Scenario: Ordinary shot response
- **WHEN** player A's shot does not complete the game
- **THEN** the response has `state` `Started`
