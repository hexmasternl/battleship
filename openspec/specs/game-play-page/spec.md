# game-play-page Specification

## Purpose
Defines the game page while a game is being played: the two boards and their tabs, how the page follows the turn, how a player selects, confirms and fires a shot, how shot results and the opponent's shots appear, and how the page stays correct after a refresh or a dropped event stream.

## Requirements

### Requirement: A started game shows the game play view
When the game loaded on `/games/{game-code}` is `Started`, the page SHALL show the game play view, built from the caller's view of the game: the caller's ships, the shots fired by the caller with their results, the shots fired by the opponent with their results, and who has the turn. When a `game.started.v1` event for this game arrives while the placement view is shown, the page SHALL switch to the game play view without a reload.

#### Scenario: Refresh during play
- **WHEN** a player reloads `/games/K7Q2M9XA` while the game is `Started` and three shots have been fired by each player
- **THEN** the game play view shows the player's ships, all six shots with their hit or miss markers, and the current turn

#### Scenario: Game starts while on the page
- **WHEN** a player is on the placement view and a `game.started.v1` event for this game arrives
- **THEN** the game play view is shown with the turn given in the event

### Requirement: Two tabs show the two boards
The game play view SHALL show two tabs, "You" and "Opponent". "You" SHALL show a 12x12 grid of the opponent's waters with only the shots fired by the player: a light grey dot for a miss and a red dot for a hit. "Opponent" SHALL show a 12x12 grid of the player's own board with the player's ships and the shots fired by the opponent, marked the same way. The opponent's ships SHALL never be shown.

#### Scenario: You tab content
- **WHEN** the player has fired a hit at C5 and a miss at D9 and opens the "You" tab
- **THEN** the grid shows a red hit marker at C5, a grey miss marker at D9, no other markers and no ships

#### Scenario: Opponent tab content
- **WHEN** the opponent has fired a hit on the player's carrier and the player opens the "Opponent" tab
- **THEN** the grid shows all five of the player's ships and a red hit marker on the carrier cell

#### Scenario: Markers are not colour only
- **WHEN** a hit and a miss marker are shown side by side
- **THEN** they differ by a non-colour cue as well as by colour, and each cell exposes its coordinate and state to assistive technology

### Requirement: The tabs follow the turn
The page SHALL select "You" when the player has the turn and "Opponent" when the opponent has the turn, both when the game play view first opens and every time the turn changes. The player MAY switch tabs by hand in between. The page SHALL state in text whose turn it is.

#### Scenario: Opening on my turn
- **WHEN** the game play view opens and the player has the turn
- **THEN** the "You" tab is selected and the page says it is the player's turn

#### Scenario: Turn passes to the opponent
- **WHEN** a `game.turn-changed.v1` event for this game names the opponent as the player with the turn
- **THEN** the "Opponent" tab is selected and the page says it is the opponent's turn

#### Scenario: Turn comes back
- **WHEN** the player viewed "You" by hand during the opponent's turn and a `game.turn-changed.v1` event names the player
- **THEN** the "You" tab stays selected and the player can choose a cell

### Requirement: A shot is selected and confirmed before it is fired
While the player has the turn and no shot is in flight, every cell of the "You" grid the player has not fired at SHALL be selectable by tap, click or keyboard. Selecting a cell SHALL mark it as the target and show an interactive snackbar naming the cell with "Cancel" and "Shoot" actions, on every device. "Cancel" SHALL clear the target and send nothing; selecting another cell SHALL move the target; "Shoot" SHALL fire the shot.

#### Scenario: Confirmed shot
- **WHEN** the player has the turn, selects E7 and chooses "Shoot"
- **THEN** exactly one shot request for E7 is sent and the snackbar closes

#### Scenario: Cancelled shot
- **WHEN** the player selects E7 and chooses "Cancel"
- **THEN** no request is sent, the target mark is removed and the player still has the turn

#### Scenario: Target moves
- **WHEN** the player selects E7 and then selects F2 before choosing an action
- **THEN** F2 is the only marked target and the snackbar names F2

#### Scenario: Already shot cell
- **WHEN** the player has the turn and activates a cell they already fired at
- **THEN** no target is set and no snackbar is shown

#### Scenario: Without the turn
- **WHEN** the opponent has the turn and the player activates a cell on the "You" grid
- **THEN** no target is set and no snackbar is shown

### Requirement: The shot result is shown when the server answers
When the server accepts a shot, the page SHALL add a hit or miss marker at the cell as the response reports it, animate the new marker into place, and accept no further shot until a turn change gives the player the turn again. While the request is in flight no other shot SHALL be possible. When the server refuses the shot or the request fails, the page SHALL show a message and leave the cell without a marker; after a `409` refusal it SHALL reload the game state.

#### Scenario: Hit result
- **WHEN** the player fires at E7 and the server answers with `hit` true
- **THEN** a red hit marker animates in at E7 and no further cell is selectable

#### Scenario: Refused shot
- **WHEN** the player fires at E7 and the server answers `409 Conflict` because it is not the player's turn
- **THEN** a message explains the refusal, E7 has no marker, and the page reloads the game state and shows the turn the server reports

#### Scenario: Network failure
- **WHEN** the shot request fails without a response
- **THEN** a message says the shot was not fired, E7 has no marker and the player can select a cell again

### Requirement: Opponent shots appear live
When a `game.shot-fired.v1` event for this game arrives with the opponent as the shooter, the page SHALL add the marker at that cell on the "Opponent" board with the event's hit flag and animate it into place. Events for other games SHALL be ignored. A shot already shown at the same cell for the same shooter SHALL NOT be added again, so the player's own shot event and replayed events do not create duplicates.

#### Scenario: Opponent fires
- **WHEN** a `game.shot-fired.v1` event for this game reports the opponent hitting B2
- **THEN** a red hit marker animates in at B2 on the "Opponent" board

#### Scenario: Own shot event
- **WHEN** the player's shot at E7 was applied from the response and the matching `game.shot-fired.v1` event arrives
- **THEN** the "You" board still has exactly one marker at E7

#### Scenario: Other game
- **WHEN** a `game.shot-fired.v1` event for a different game code arrives
- **THEN** neither board changes

### Requirement: The view resynchronises after a dropped stream
When the event stream returns to open after reconnecting, the page SHALL reload the game state and replace its boards and turn with the server's view, so shots and turn changes missed while disconnected are shown.

#### Scenario: Missed opponent shot
- **WHEN** the stream drops, the opponent fires, and the stream reopens without replaying that event
- **THEN** after the reopen the "Opponent" board shows the opponent's shot and the page shows that the player has the turn

### Requirement: New markers respect reduced motion
The marker animation SHALL use the design system's motion tokens and SHALL be replaced by an immediate appearance when the user prefers reduced motion.

#### Scenario: Reduced motion
- **WHEN** the operating system reports `prefers-reduced-motion: reduce` and a shot result arrives
- **THEN** the marker appears in its final state without animating

### Requirement: The game play view is mobile first
The game play view SHALL lay out for a 360px wide viewport first, with the turn indicator, the tabs and the grid stacked and the whole 12x12 grid visible without horizontal page scrolling. It SHALL take every colour, size and motion value from the design system tokens through the central SASS variables.

#### Scenario: Narrow phone
- **WHEN** the game play view is shown on a 360px wide viewport
- **THEN** all 144 cells of the selected grid are visible and the page does not scroll horizontally
