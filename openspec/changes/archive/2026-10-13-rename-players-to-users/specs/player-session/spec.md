# Spec Delta

## REMOVED Requirements

### Requirement: Refresh tokens persist across browser sessions
**Reason**: The frontend's identity state belongs to the Users domain and is renamed to the user session.
**Migration**: Moved unchanged in substance to `user-session` ("Refresh tokens persist across browser sessions").

### Requirement: Access tokens last only for the browser session
**Reason**: The frontend's identity state belongs to the Users domain and is renamed to the user session.
**Migration**: Moved unchanged in substance to `user-session` ("Access tokens last only for the browser session").

### Requirement: The landing page restores a returning player
**Reason**: A returning visitor is a returning user; the refresh goes to the Users service.
**Migration**: Replaced by `user-session` "The landing page restores a returning user".

### Requirement: A shared component asks for a player name and creates the player
**Reason**: The component creates a user with a display name; only its visible label still says "Player name".
**Migration**: Replaced by `user-session` "A shared component asks for a display name and creates the user".

### Requirement: A player is known when the frontend holds their tokens
**Reason**: Being known is a property of the user session.
**Migration**: Replaced by `user-session` "A user is known when the frontend holds their tokens".
