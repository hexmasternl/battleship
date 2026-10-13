# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: Creating a game requires a known player`
- TO: `### Requirement: Creating a game requires a known user`

## MODIFIED Requirements

### Requirement: Creating a game requires a known user
The Games API SHALL expose `POST /games`, reachable from the frontend as `/api/games` through the gateway. It SHALL accept a request only with a valid access token issued by the Users service, and SHALL respond `401 Unauthorized` without creating anything when the token is missing, expired or invalid.

#### Scenario: No token
- **WHEN** `POST /games` is called without an `Authorization` header
- **THEN** the response is `401 Unauthorized` and no game is stored

#### Scenario: Expired token
- **WHEN** `POST /games` is called with an access token whose expiry has passed
- **THEN** the response is `401 Unauthorized` and no game is stored

### Requirement: The creator is taken from the token
The Games API SHALL make the calling user player one of the new game: the player id SHALL be the user id from the token subject, and the player name SHALL be the user's display name from the token name claim. It SHALL ignore any player identity sent in the request body.

#### Scenario: Identity from the token
- **WHEN** user A with display name `Admiral Nelson` calls `POST /games` with A's access token
- **THEN** the new game's player one has player id equal to A's user id and the player name `Admiral Nelson`
