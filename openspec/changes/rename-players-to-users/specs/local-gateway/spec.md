# Spec Delta

## MODIFIED Requirements

### Requirement: The AppHost runs a gateway in front of the module APIs
The AppHost SHALL start a gateway resource named `gateway` on every local run. The gateway SHALL start only after every module API it routes to has started, and SHALL report on the Aspire dashboard with an HTTP endpoint.

#### Scenario: Gateway starts after the APIs
- **WHEN** the AppHost is started locally
- **THEN** the dashboard shows a `gateway` resource that waits for the Users and Games APIs and only then reaches the running state with an HTTP endpoint

### Requirement: Internal endpoints are not exposed through the gateway
Endpoints meant only for the platform or for diagnostics, such as Dapr subscription routes and smoke endpoints, SHALL NOT be reachable through the gateway.

#### Scenario: Smoke endpoint is not reachable through the gateway
- **WHEN** a client sends `POST <gateway>/smoke/publish`
- **THEN** the gateway answers `404 Not Found` and the Users API does not receive the request

## REMOVED Requirements

### Requirement: Requests are routed by module prefix without rewriting the path
**Reason**: Its scenario named the Players API, which is renamed to the Users API.
**Migration**: Replaced by "Requests are routed by module prefix with the path unchanged", identical except for the module name.

## ADDED Requirements

### Requirement: Requests are routed by module prefix with the path unchanged
The gateway SHALL forward a request whose first path segment is a declared module prefix to that module's API. The path and query string SHALL arrive unchanged.

#### Scenario: Users request reaches the Users API
- **WHEN** a client sends `POST <gateway>/users/tokens/refresh?x=1`
- **THEN** the Users API receives `POST /users/tokens/refresh?x=1`

#### Scenario: Games request reaches the Games API
- **WHEN** a client sends `GET <gateway>/games/anything`
- **THEN** the Games API receives `GET /games/anything`
