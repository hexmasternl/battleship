# Spec Delta

## MODIFIED Requirements

### Requirement: Joining a game requires a known player
The Games API SHALL expose `POST /games/{game-code}/join`, reachable from the frontend as `/api/games/{game-code}/join` through the gateway. It SHALL accept a request only with a valid access token issued by the Users service, and SHALL respond `401 Unauthorized` without changing any game when the token is missing, expired or invalid.

#### Scenario: No token
- **WHEN** `POST /games/K7Q2M9XA/join` is called without an `Authorization` header
- **THEN** the response is `401 Unauthorized` and game `K7Q2M9XA` is unchanged
