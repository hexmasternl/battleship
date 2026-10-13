# Spec Delta

## MODIFIED Requirements

### Requirement: Requests are routed by module prefix without rewriting the path
The gateway SHALL forward a request whose first path segment is a declared module prefix to that module's API. The path and query string SHALL arrive unchanged.

#### Scenario: Players request reaches the Players API
- **WHEN** a client sends `POST <gateway>/users/tokens/refresh?x=1`
- **THEN** the Players API receives `POST /users/tokens/refresh?x=1`

#### Scenario: Games request reaches the Games API
- **WHEN** a client sends `GET <gateway>/games/anything`
- **THEN** the Games API receives `GET /games/anything`
