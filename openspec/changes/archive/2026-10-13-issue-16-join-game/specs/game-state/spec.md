# Spec Delta

## ADDED Requirements

### Requirement: Concurrent updates never overwrite each other
The Games service SHALL store an update to a game only when the game has not been changed since it was read. When another update was stored in between, the service SHALL read the game again, re-apply the change to the fresh state and store it, with at most three attempts in total. When all attempts conflict, the request SHALL fail with `409 Conflict` without storing the change.

#### Scenario: Conflicting update is re-applied
- **WHEN** two requests read the same game and both store a change, and the second write finds the game changed by the first
- **THEN** the second request reads the game again, applies its change to the state that contains the first change, and stores it, so both changes are kept

#### Scenario: Re-applied change sees the new state
- **WHEN** a change is re-applied after a conflict and the fresh state no longer allows it
- **THEN** the change is refused with the same response the request would have received had it read the fresh state first

#### Scenario: Retries are bounded
- **WHEN** three attempts to store a change to one game all find the game changed
- **THEN** the request fails with `409 Conflict` and the game holds only the changes that other requests stored
