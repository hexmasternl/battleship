# Spec Delta

## MODIFIED Requirements

### Requirement: Calls to action navigate to the game routes
Activating "Create a new game" SHALL, for a known player, open a modal dialog asking the player to confirm creating a new game. Confirming SHALL create the game and navigate to `/games/{game-code}` of the new game; cancelling SHALL leave the visitor on the landing page and create nothing. Activating "Join an existing game" SHALL lead to `/games/join`, and for a known player SHALL navigate straight away. When the player is unknown, either call to action SHALL first open a modal dialog containing the shared player-name component; after that component has created the player, "Create a new game" SHALL continue with the confirmation dialog and "Join an existing game" SHALL navigate. Dismissing either dialog SHALL leave the visitor on the landing page. When a landing page refresh is still pending, the call to action SHALL wait for it before deciding. Until the game page exists, `/games/{game-code}` SHALL render a placeholder panel naming the game code, and `/games/join` SHALL render a placeholder panel stating that the feature is coming soon.

#### Scenario: Create a new game
- **WHEN** a known player activates "Create a new game" with a pointer or the keyboard, confirms in the dialog, and the game is created with code `K7Q2M9XA`
- **THEN** the dialog closes, the router navigates to `/games/K7Q2M9XA` and the placeholder panel naming `K7Q2M9XA` is shown

#### Scenario: Confirmation dialog for a known player
- **WHEN** a known player activates "Create a new game"
- **THEN** the router does not navigate, a modal dialog opens asking to create a new game with a confirm and a cancel action, and focus moves into the dialog

#### Scenario: Creation cancelled
- **WHEN** the player cancels the confirmation dialog or presses Escape
- **THEN** no request to create a game is sent, the router does not navigate, and focus returns to "Create a new game"

#### Scenario: Creation fails
- **WHEN** the player confirms and the create request fails
- **THEN** the dialog stays open, shows an error with a way to try again, and the router does not navigate

#### Scenario: Confirm cannot be sent twice
- **WHEN** the player confirms and the create request is still pending
- **THEN** the confirm action is disabled and a second activation sends no second request

#### Scenario: Join an existing game
- **WHEN** a known player activates "Join an existing game" with a pointer or the keyboard
- **THEN** no dialog opens, the router navigates to `/games/join` and the placeholder panel is shown

#### Scenario: Unknown player is asked for a name
- **WHEN** an unknown visitor activates "Create a new game" or "Join an existing game"
- **THEN** the router does not navigate, a modal dialog opens with the player-name component, and focus moves into the dialog

#### Scenario: Navigation after the player is created
- **WHEN** the unknown visitor enters a valid player name in the dialog opened by "Join an existing game" and the player is created
- **THEN** the dialog closes and the router navigates to `/games/join`

#### Scenario: Confirmation after the player is created
- **WHEN** the unknown visitor enters a valid player name in the dialog opened by "Create a new game" and the player is created
- **THEN** the player-name dialog closes and the confirmation dialog opens

#### Scenario: Dialog dismissed
- **WHEN** the visitor closes the dialog with its cancel action or the Escape key without creating a player
- **THEN** the router does not navigate, no tokens are stored, and focus returns to the call to action that opened the dialog

#### Scenario: Pending refresh is awaited
- **WHEN** a returning player activates a call to action while the landing page refresh request is still pending and that refresh then succeeds
- **THEN** no player-name dialog opens, and the call to action continues as for a known player
