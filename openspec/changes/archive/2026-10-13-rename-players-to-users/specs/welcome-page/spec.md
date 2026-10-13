# Spec Delta

## MODIFIED Requirements

### Requirement: Calls to action navigate to the game routes
Activating "Create a new game" SHALL, for a known user, open a modal dialog asking to confirm creating a new game. Confirming SHALL create the game and navigate to `/games/{game-code}` of the new game; cancelling SHALL leave the visitor on the landing page and create nothing. Activating "Join an existing game" SHALL, for a known user, open a modal dialog asking for a game code. Joining SHALL navigate to `/games/{game-code}` of the joined game; cancelling SHALL leave the visitor on the landing page and join nothing. The game code input SHALL accept exactly 8 characters `A`-`Z` and `0`-`9` and SHALL show typed letters in upper case. When the game is not found, is full or has already started, the join dialog SHALL stay open and name that reason. When the user is unknown, either call to action SHALL first open a modal dialog containing the shared display-name component; after that component has created the user, "Create a new game" SHALL continue with the confirmation dialog and "Join an existing game" SHALL continue with the join dialog. Dismissing any of these dialogs SHALL leave the visitor on the landing page. When a landing page refresh is still pending, the call to action SHALL wait for it before deciding. Until the game page exists, `/games/{game-code}` SHALL render a placeholder panel naming the game code.

#### Scenario: Create a new game
- **WHEN** a known user activates "Create a new game" with a pointer or the keyboard, confirms in the dialog, and the game is created with code `K7Q2M9XA`
- **THEN** the dialog closes, the router navigates to `/games/K7Q2M9XA` and the placeholder panel naming `K7Q2M9XA` is shown

#### Scenario: Confirmation dialog for a known player
- **WHEN** a known user activates "Create a new game"
- **THEN** the router does not navigate, a modal dialog opens asking to create a new game with a confirm and a cancel action, and focus moves into the dialog

#### Scenario: Creation cancelled
- **WHEN** the user cancels the confirmation dialog or presses Escape
- **THEN** no request to create a game is sent, the router does not navigate, and focus returns to "Create a new game"

#### Scenario: Creation fails
- **WHEN** the user confirms and the create request fails
- **THEN** the dialog stays open, shows an error with a way to try again, and the router does not navigate

#### Scenario: Confirm cannot be sent twice
- **WHEN** the user confirms and the create request is still pending
- **THEN** the confirm action is disabled and a second activation sends no second request

#### Scenario: Join dialog for a known player
- **WHEN** a known user activates "Join an existing game" with a pointer or the keyboard
- **THEN** the router does not navigate, a modal dialog opens with a game code input, a join action and a cancel action, and focus moves to the game code input

#### Scenario: Join an existing game
- **WHEN** a known user enters `K7Q2M9XA` in the join dialog and joins, and the game accepts the user
- **THEN** the dialog closes, the router navigates to `/games/K7Q2M9XA` and the placeholder panel naming `K7Q2M9XA` is shown

#### Scenario: Game code is upper-cased and limited
- **WHEN** the user types `k7q2m9xa` in the game code input
- **THEN** the input shows `K7Q2M9XA`, and the join action stays disabled while the code is not exactly 8 characters `A`-`Z` or `0`-`9`

#### Scenario: Game not found
- **WHEN** the user joins with a code the backend does not know
- **THEN** the dialog stays open, states that no game with that code was found, keeps the entered code for correction, and the router does not navigate

#### Scenario: Game full
- **WHEN** the user joins a game that already has two other players
- **THEN** the dialog stays open, states that the game is full, and the router does not navigate

#### Scenario: Game already started
- **WHEN** the user joins a game that has already started without them
- **THEN** the dialog stays open, states that the game has already started, and the router does not navigate

#### Scenario: Join fails unexpectedly
- **WHEN** the join request fails for another reason, such as a network error
- **THEN** the dialog stays open, shows an error with a way to try again, and the router does not navigate

#### Scenario: Join cannot be sent twice
- **WHEN** the user joins and the join request is still pending
- **THEN** the join action is disabled and a second activation sends no second request

#### Scenario: Join cancelled
- **WHEN** the user cancels the join dialog or presses Escape
- **THEN** no join request is sent, the router does not navigate, and focus returns to "Join an existing game"

#### Scenario: Unknown player is asked for a name
- **WHEN** an unknown visitor activates "Create a new game" or "Join an existing game"
- **THEN** the router does not navigate, a modal dialog opens with the display-name component labelled "Player name", and focus moves into the dialog

#### Scenario: Join dialog after the player is created
- **WHEN** the unknown visitor enters a valid name in the dialog opened by "Join an existing game" and the user is created
- **THEN** the display-name dialog closes and the join dialog opens

#### Scenario: Confirmation after the player is created
- **WHEN** the unknown visitor enters a valid name in the dialog opened by "Create a new game" and the user is created
- **THEN** the display-name dialog closes and the confirmation dialog opens

#### Scenario: Dialog dismissed
- **WHEN** the visitor closes the dialog with its cancel action or the Escape key without creating a user
- **THEN** the router does not navigate, no tokens are stored, and focus returns to the call to action that opened the dialog

#### Scenario: Pending refresh is awaited
- **WHEN** a returning user activates a call to action while the landing page refresh request is still pending and that refresh then succeeds
- **THEN** no display-name dialog opens, and the call to action continues as for a known user
