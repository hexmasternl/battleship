# Spec Delta

## Purpose

Defines the frontend placement phase of a game: the board and ship list, placing, moving and rotating ships by drag and drop, tap or keyboard, saving every change, the players panel, the ready action and the responsive layout.

## ADDED Requirements

### Requirement: The screen shows the board and the fleet
The placement screen SHALL show a 12x12 grid with column labels A to L and row labels 1 to 12, the caller's placed ships on the grid, and a ship list with every ship not yet placed. Each ship in the list SHALL show its name and its length as cells, so all sizes are visibly different.

#### Scenario: Nothing placed yet
- **WHEN** player A opens the placement screen with no ships placed
- **THEN** the grid is empty and the ship list shows Carrier 5, Battleship 4, Cruiser 3, Submarine 3 and Destroyer 2, each drawn at its length

#### Scenario: Some ships placed
- **WHEN** player A has placed the carrier and the cruiser
- **THEN** both appear on the grid and the ship list shows only the battleship, the submarine and the destroyer

### Requirement: Ships are placed and moved by drag and drop
A ship SHALL be dragged from the ship list onto the grid, and a placed ship SHALL be dragged to another position on the grid. While a ship is dragged over the grid, the cells it would cover SHALL show whether the placement is valid. A drop at a valid position SHALL place the ship there with its current orientation; a drop at an invalid position or outside the grid SHALL leave the ship where it was. A dragged ship SHALL keep its orientation; a ship from the list is horizontal.

#### Scenario: Drag from the list
- **WHEN** player A drags the destroyer from the list and drops it with its first cell on C4
- **THEN** the destroyer covers C4 and D4 and leaves the list

#### Scenario: Invalid drop
- **WHEN** player A drops the battleship where it would overlap the carrier
- **THEN** the battleship stays where it was before the drag and the overlap is shown as invalid while dragging

### Requirement: Ships can be placed without dragging
Every action possible by drag and drop SHALL also be possible by tap or click and by keyboard: selecting a ship and then selecting a grid cell SHALL place or move the selected ship with its origin on that cell. Grid cells and ships SHALL be focusable and operable with the keyboard, and each cell SHALL expose its coordinate to assistive technology.

#### Scenario: Tap to place
- **WHEN** player A taps the submarine in the list and then taps cell F6
- **THEN** the submarine is placed with its origin on F6, horizontal

#### Scenario: Keyboard move
- **WHEN** player A focuses the placed cruiser, activates it with Enter, focuses cell A1 and activates it with Enter
- **THEN** the cruiser moves to origin A1 with its orientation unchanged

### Requirement: The active ship can be rotated
Selecting a ship SHALL make it the active ship, shown with a border, with at most one active ship at a time. A placed active ship SHALL show a rotate control that turns it 90° clockwise, switching between horizontal and vertical while its origin stays fixed. A rotation that would leave the board or overlap another ship SHALL be refused, the ship SHALL stay as it was, and the player SHALL see why.

#### Scenario: Rotate a ship
- **WHEN** the cruiser at origin B2 horizontal is active and the player activates the rotate control
- **THEN** the cruiser covers B2, B3 and B4 vertically

#### Scenario: Rotation refused at the edge
- **WHEN** the carrier at origin A10 horizontal is active and the player rotates it
- **THEN** the carrier stays horizontal and a message says it would leave the board

#### Scenario: One active ship
- **WHEN** the cruiser is active and the player selects the destroyer
- **THEN** the destroyer is active and the cruiser has no border and no rotate control

### Requirement: Every placement change is saved on the server
Each accepted drop, tap placement, keyboard placement and rotation SHALL be sent to the server as one placement of that ship. The screen SHALL show the change at once and keep it when the server accepts it. When the server refuses the change or the request fails, the screen SHALL show a message and reload the game, so the board matches the server again.

#### Scenario: Saved placement
- **WHEN** player A drops the destroyer on C4 and the server accepts it
- **THEN** exactly one placement request for the destroyer at column 2, row 3, horizontal was sent and the destroyer stays on C4

#### Scenario: Refused placement
- **WHEN** the server refuses a placement with `409 Conflict`
- **THEN** a message is shown and the board shows the ships the server holds

### Requirement: The players panel shows both players
The screen SHALL show a players panel listing the caller, marked as such, and the opponent, each with name and ready state (ready or not ready) shown by text as well as colour. While there is no second player, the panel SHALL show "Player 2" greyed out. The panel SHALL update when the game is reloaded after a join or ready event.

#### Scenario: Waiting for an opponent
- **WHEN** player A is alone in the game
- **THEN** the panel shows A with "not ready" and a greyed-out "Player 2"

#### Scenario: Opponent becomes ready
- **WHEN** player B becomes ready while A is on the placement screen
- **THEN** A's panel shows B as ready without a manual reload

### Requirement: The player declares ready once the fleet is placed
The screen SHALL show a "Ready" action that is enabled only when all five ships are placed and no save is pending. Activating it SHALL send the ready request. While it is in flight a second ready SHALL NOT be sent. After the server confirms, the board SHALL be read only: no drag, selection, rotation or placement, and the ready action is replaced by a waiting message until the game starts.

#### Scenario: Ready disabled
- **WHEN** player A has placed four ships
- **THEN** the "Ready" action is disabled

#### Scenario: Ready confirmed
- **WHEN** player A has placed all five ships, activates "Ready" and the server confirms
- **THEN** A is shown as ready, the ships can no longer be moved or rotated, and the screen says it is waiting for the opponent

#### Scenario: Ready after refresh
- **WHEN** a ready player reloads the page while the game is still `New`
- **THEN** the board is read only and the player is shown as ready

### Requirement: The placement screen is mobile first
The screen SHALL lay out for a 360px wide viewport first, stacking the players panel, the grid and the ship list in that order, with the ships of the list under each other and the whole grid visible without horizontal page scrolling. On wide viewports it SHALL show the grid on the left, the players panel top right and the ship list bottom right. All colours, sizes, spacing and motion SHALL come from the design system tokens through the central SASS variables.

#### Scenario: Narrow phone
- **WHEN** the placement screen is shown on a 360px wide viewport
- **THEN** players, grid and ships are stacked in that order and all 144 cells are visible without horizontal scrolling

#### Scenario: Wide screen
- **WHEN** the placement screen is shown on a 1280px wide viewport
- **THEN** the grid is on the left, the players panel is top right and the ship list is bottom right
