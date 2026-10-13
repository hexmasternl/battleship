# game-page Specification

## Purpose

Defines the frontend page for one game at `/games/{game-code}`: who may open it, how it loads the game from the server and handles a missing game, how it stays current through the event stream and after a refresh, and which phase it shows for each game state.

## Requirements

### Requirement: The game page requires a known player
When `/games/{game-code}` is opened and the local session holds no player, the frontend SHALL navigate to the landing page without requesting the game.

#### Scenario: No player in the session
- **WHEN** a visitor without a stored player opens `/games/K7Q2M9XA`
- **THEN** the landing page is shown and no game request is sent

### Requirement: The game page loads the game from the server
With a known player, the page SHALL request the caller's view of the game for the code in the URL and show a loading indicator until it arrives. A `404` SHALL show a not-found message with a way back to the landing page. Any other failure SHALL show an error message with a retry action.

#### Scenario: Game found
- **WHEN** player A opens `/games/K7Q2M9XA` and the server returns A's view of a `New` game
- **THEN** the placement screen is shown with A's ships from the view

#### Scenario: Game not found
- **WHEN** player C opens `/games/K7Q2M9XA` and the server answers `404`
- **THEN** the page says that the game was not found and offers a link to the landing page

#### Scenario: Load fails
- **WHEN** the game request fails with a network error
- **THEN** the page shows an error with a retry action, and retrying requests the game again

### Requirement: The page shows the phase that matches the game state
The page SHALL show the placement screen while the game is `New`. While the game is `Started` it SHALL show a "game started" message until the game play view exists. For `Completed` and `Abandoned` it SHALL show that the game is over with a way back to the landing page.

#### Scenario: Started game
- **WHEN** the loaded game is `Started`
- **THEN** the page shows the game started message and no placement controls

### Requirement: The page can be refreshed at any moment
Everything the page shows SHALL be derived from the server's view of the game. Reloading the page at any moment SHALL show the same players, ships, ready states and phase as before the reload.

#### Scenario: Refresh during placement
- **WHEN** player A has placed three ships, reloads `/games/K7Q2M9XA` and the server holds those three placements
- **THEN** the placement screen shows the same three ships at the same cells and orientations, and the other two in the ship list

### Requirement: The page follows the game live
On opening, the page SHALL connect the event stream for the current player. When a `game.joined.v1`, `game.player-ready.v1` or `game.started.v1` event for this game's code arrives, the page SHALL reload the game state. Events for other games SHALL be ignored. When the stream returns to open after reconnecting, the page SHALL reload the game state.

#### Scenario: Opponent joins
- **WHEN** player A is on the page of game `K7Q2M9XA` and a `game.joined.v1` event for `K7Q2M9XA` arrives
- **THEN** the page reloads the game and the players panel shows the second player's name

#### Scenario: Game starts
- **WHEN** a `game.started.v1` event for this game arrives while the placement screen is shown
- **THEN** the page reloads the game and shows the game started phase

#### Scenario: Event for another game
- **WHEN** a `game.player-ready.v1` event for game `ZZZZ9999` arrives
- **THEN** the page does not reload
