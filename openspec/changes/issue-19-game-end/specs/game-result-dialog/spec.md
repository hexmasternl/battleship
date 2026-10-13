# Spec Delta

## Purpose

Defines how the game page presents a finished game to a player: when it switches to the ended view, the read-only final boards, the dialog that tells the player whether they won or lost, and the way back to the landing page.

## ADDED Requirements

### Requirement: The game page shows the end of the game
The game page SHALL switch to its ended view when, for the game it shows, the event stream delivers `game.ended.v1` with that game's code, the player's own shot response reports state `Completed`, or the loaded game is `Completed`. A `game.ended.v1` event for another game code SHALL be ignored. Receiving the end more than once SHALL have the same effect as receiving it once.

#### Scenario: End event for this game
- **WHEN** the page shows game `ABCD1234` and the stream delivers `game.ended.v1` with game code `ABCD1234`
- **THEN** the page switches to its ended view and opens the result dialog

#### Scenario: End event for another game
- **WHEN** the page shows game `ABCD1234` and the stream delivers `game.ended.v1` with game code `ZZZZ9999`
- **THEN** the page is unchanged and no dialog opens

#### Scenario: Own winning shot without the event stream
- **WHEN** the player's shot response reports state `Completed` while the event stream is disconnected
- **THEN** the page switches to its ended view with the player as the winner and opens the result dialog

#### Scenario: Response and event both arrive
- **WHEN** the player's winning shot response and the `game.ended.v1` event for the same game both arrive
- **THEN** exactly one result dialog is open

#### Scenario: Completed game is loaded
- **WHEN** a player opens or refreshes `/games/{game-code}` of a game that is `Completed`
- **THEN** the page shows the final boards and the result dialog

### Requirement: The final boards stay visible and read only
In the ended view the page SHALL keep showing both boards as they stand after the last shot, including the winning shot. No cell SHALL be selectable, no shot confirmation SHALL be offered, no shot SHALL be sent, no player SHALL be shown as having the turn, and the boards SHALL NOT switch tabs on their own.

#### Scenario: Final boards behind the dialog
- **WHEN** the result dialog is open
- **THEN** both final boards are visible behind it with every shot marked hit or miss, and the winning shot is shown as a hit

#### Scenario: No shooting after the end
- **WHEN** the player activates a cell of the opponent's board in the ended view
- **THEN** no shot confirmation appears and no shot request is sent

### Requirement: The result dialog tells the player the outcome
The result dialog SHALL be modal and SHALL state the outcome for the viewing player: "You won" when the player is the winner, "You lost" when the other player is the winner, and a neutral "The game has ended" message when the game has no winner. It SHALL offer one "Close" action and SHALL move focus into the dialog when it opens.

#### Scenario: Winner sees the win
- **WHEN** the game ends with player A as the winner and A's page shows the dialog
- **THEN** the dialog says "You won" and focus is inside the dialog

#### Scenario: Loser sees the loss
- **WHEN** the game ends with player A as the winner and B's page shows the dialog
- **THEN** the dialog says "You lost"

#### Scenario: Game ended without a winner
- **WHEN** the game ends without a winner
- **THEN** the dialog shows the neutral "The game has ended" message, not a win or a loss

### Requirement: Closing the result returns to the landing page
Activating "Close" with a pointer or the keyboard, or dismissing the dialog with Escape, SHALL close the dialog and navigate to the landing page.

#### Scenario: Close
- **WHEN** the player activates "Close" in the result dialog
- **THEN** the dialog closes and the router navigates to `/`

#### Scenario: Escape
- **WHEN** the player presses Escape while the result dialog is open
- **THEN** the dialog closes and the router navigates to `/`
