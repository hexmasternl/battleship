# Spec Delta

## MODIFIED Requirements

### Requirement: Calls to action navigate to the game routes
Activating "Create a new game" SHALL lead to `/games/new` and activating "Join an existing game" SHALL lead to `/games/join`. When the player is known, the call to action SHALL navigate straight away. When the player is unknown, it SHALL first open a modal dialog containing the shared player-name component and SHALL navigate only after that component has created the player; dismissing the dialog SHALL leave the visitor on the landing page. When a landing page refresh is still pending, the call to action SHALL wait for it before deciding. Until the game flows exist, both routes SHALL render a placeholder panel stating that the feature is coming soon.

#### Scenario: Create a new game
- **WHEN** a known player activates "Create a new game" with a pointer or the keyboard
- **THEN** no dialog opens, the router navigates to `/games/new` and the placeholder panel is shown

#### Scenario: Join an existing game
- **WHEN** a known player activates "Join an existing game" with a pointer or the keyboard
- **THEN** no dialog opens, the router navigates to `/games/join` and the placeholder panel is shown

#### Scenario: Unknown player is asked for a name
- **WHEN** an unknown visitor activates "Create a new game" or "Join an existing game"
- **THEN** the router does not navigate, a modal dialog opens with the player-name component, and focus moves into the dialog

#### Scenario: Navigation after the player is created
- **WHEN** the unknown visitor enters a valid player name in the dialog opened by "Join an existing game" and the player is created
- **THEN** the dialog closes and the router navigates to `/games/join`

#### Scenario: Dialog dismissed
- **WHEN** the visitor closes the dialog with its cancel action or the Escape key without creating a player
- **THEN** the router does not navigate, no tokens are stored, and focus returns to the call to action that opened the dialog

#### Scenario: Pending refresh is awaited
- **WHEN** a returning player activates a call to action while the landing page refresh request is still pending and that refresh then succeeds
- **THEN** no dialog opens and the router navigates to the call to action's route
