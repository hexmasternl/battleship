# game-retention Specification

## Purpose

Defines how long the Games service keeps a game after its last activity, how inactive games are removed, and how that removal is triggered and observed.

## Requirements

### Requirement: Inactive games are removed after the retention period
The Games service SHALL remove every stored game whose last activity instant is more than the retention period before the current UTC time, whatever its state. The retention period SHALL default to 90 days and SHALL be configurable. Games whose last activity is within the retention period SHALL be left unchanged.

#### Scenario: Game past the retention period is removed
- **WHEN** the cleanup runs and a game's last activity was 91 days ago
- **THEN** the game is removed

#### Scenario: Game within the retention period is kept
- **WHEN** the cleanup runs and a game's last activity was 89 days ago
- **THEN** the game is unchanged

#### Scenario: State does not matter
- **WHEN** the cleanup runs and there are games in states `New`, `Started`, `Completed` and `Abandoned`, all with last activity 120 days ago
- **THEN** all of them are removed

#### Scenario: Retention is configurable
- **WHEN** the retention period is configured as 30 days and the cleanup runs while a game's last activity was 31 days ago
- **THEN** the game is removed

### Requirement: A removed game is gone entirely
Removing a game SHALL delete everything the Games service stores for it. Afterwards, every request for that game code SHALL be answered as for an unknown game.

#### Scenario: Removed game is not found
- **WHEN** a game has been removed by the cleanup and a player of that game requests it by its code
- **THEN** the response is `404 Not Found` with the game-not-found problem type

### Requirement: Cleanup selects candidates without reading game documents
The cleanup SHALL select candidate games by their last activity column alone. It SHALL NOT read or deserialize the stored game document of any game.

#### Scenario: Unreadable document does not stop the cleanup
- **WHEN** an inactive game's stored document cannot be deserialized and the cleanup runs
- **THEN** that game is removed like any other inactive game

### Requirement: Cleanup is safe to repeat and to race
Running the cleanup again right after a run SHALL remove nothing and succeed. A game that disappears between selection and removal SHALL be skipped without failing the run. A game whose stored version changes between selection and removal SHALL be kept.

#### Scenario: Second run removes nothing
- **WHEN** the cleanup runs twice in a row with no other changes
- **THEN** the second run reports zero removed games and succeeds

#### Scenario: Game touched during the run is kept
- **WHEN** an inactive game is selected by the cleanup and a player changes it before it is deleted
- **THEN** the game is kept with the player's change

### Requirement: Cleanup is triggered daily through an internal endpoint
The Games API SHALL expose the cleanup as `POST /maintenance/game-cleanup`. The endpoint SHALL answer `200 OK` with the number of games removed. The local application host SHALL call this endpoint once a day through the Games API's Dapr sidecar. The endpoint SHALL NOT be reachable through the gateway.

#### Scenario: Endpoint reports the result
- **WHEN** `POST /maintenance/game-cleanup` is sent to the Games API while two games are past the retention period
- **THEN** the response is `200 OK` with a body stating that two games were removed

#### Scenario: Not reachable through the gateway
- **WHEN** a client sends `POST <gateway>/maintenance/game-cleanup`
- **THEN** the gateway answers `404 Not Found` and the Games API does not receive the request

#### Scenario: Scheduled daily
- **WHEN** the application host runs locally
- **THEN** the Games API sidecar carries a schedule that invokes the cleanup endpoint once a day

### Requirement: Cleanup runs are observable
Each cleanup run SHALL run inside its own use-case span carrying the cutoff instant and the number of games removed and skipped. Removed games SHALL be counted in a `games.removed` metric without per-game tags. Each run SHALL write one structured log line with the counts.

#### Scenario: Run shows up on the dashboard
- **WHEN** the cleanup removes three games
- **THEN** the dashboard shows a use-case span for the cleanup with three removed games, and the `games.removed` counter increases by three
