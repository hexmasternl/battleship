# game-creation Specification

## Purpose
Defines how a known player creates a new game: the request the frontend sends, how the caller is identified, what the new game looks like, what the response contains, and the integration event that announces it.

## Requirements

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

### Requirement: A new game starts empty and waiting
A created game SHALL be in state `New`, with the caller as player one, an empty player two, neither player having the turn, empty ship and shot coordinates, and last activity set to the creation instant.

#### Scenario: Initial state
- **WHEN** a known player creates a game
- **THEN** the stored game is `New`, player one is the caller, player two is empty and neither player has the turn

### Requirement: The response identifies the new game
On success the Games API SHALL respond `201 Created` with a `Location` header of `/games/{game-code}` and a JSON body containing the game id and the game code.

#### Scenario: Created response
- **WHEN** a known player creates a game whose code is `K7Q2M9XA`
- **THEN** the response is `201 Created`, `Location` is `/games/K7Q2M9XA`, and the body has `gameId` and `gameCode` `K7Q2M9XA`

### Requirement: Game creation is announced
After a game is stored, the Games service SHALL publish a `game.created.v1` integration event addressed to the creating player, carrying the game id, the game code and the creator's player id. A failure to publish SHALL be logged and SHALL NOT undo the created game or fail the response.

#### Scenario: Event is published
- **WHEN** player A creates game `K7Q2M9XA`
- **THEN** a `game.created.v1` envelope targeting player A with payload `gameId`, `gameCode` `K7Q2M9XA` and `playerId` A is published on the integration-events topic

#### Scenario: Publish failure
- **WHEN** the game is stored but publishing the event fails
- **THEN** the response is still `201 Created`, the game remains stored, and an error naming the game code is logged
