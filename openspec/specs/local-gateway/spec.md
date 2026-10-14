# local-gateway Specification

## Purpose

Defines the single backend entry point of the local development environment: a gateway that starts after the module APIs and routes each request to the module that owns its path prefix.

## Requirements

### Requirement: The AppHost runs a gateway in front of the module APIs
The AppHost SHALL start a gateway resource named `gateway` on every local run. The gateway SHALL start only after every module API it routes to has started, and SHALL report on the Aspire dashboard with an HTTP endpoint.

#### Scenario: Gateway starts after the APIs
- **WHEN** the AppHost is started locally
- **THEN** the dashboard shows a `gateway` resource that waits for the Users and Games APIs and only then reaches the running state with an HTTP endpoint

### Requirement: Every module prefix is declared explicitly
The gateway's routes SHALL come from one route table that lists, per module, its API and each of its public prefixes. A module with several prefixes SHALL list each one. No route SHALL exist for a prefix missing from the table.

#### Scenario: Undeclared prefix is not routed
- **WHEN** a client sends a request to `<gateway>/unknown/path`
- **THEN** the gateway answers `404 Not Found` and no module API receives the request

#### Scenario: Module with two prefixes
- **WHEN** the route table lists two prefixes for one module
- **THEN** requests under either prefix reach that module's API

### Requirement: Internal endpoints are not exposed through the gateway
Endpoints meant only for the platform, for diagnostics or for scheduled maintenance, such as Dapr subscription routes, smoke endpoints and endpoints under `/maintenance`, SHALL NOT be reachable through the gateway. The prefixes of such endpoints SHALL be reserved, so the route table cannot declare them for any module.

#### Scenario: Smoke endpoint is not reachable through the gateway
- **WHEN** a client sends `POST <gateway>/smoke/publish`
- **THEN** the gateway answers `404 Not Found` and the Users API does not receive the request

#### Scenario: Maintenance endpoint is not reachable through the gateway
- **WHEN** a client sends `POST <gateway>/maintenance/game-cleanup`
- **THEN** the gateway answers `404 Not Found` and the Games API does not receive the request

#### Scenario: Reserved prefix cannot be declared
- **WHEN** the route table declares `maintenance` as a prefix of any module
- **THEN** the application host refuses to start and names the module and the reserved prefix

### Requirement: Module APIs serve public endpoints under their prefix
Each module API SHALL map its public endpoints under one of the prefixes declared for it in the route table.

#### Scenario: Public endpoint lives under the module prefix
- **WHEN** a module API exposes a public endpoint
- **THEN** its route starts with a prefix the route table declares for that module

### Requirement: Requests are routed by module prefix with the path unchanged
The gateway SHALL forward a request whose first path segment is a declared module prefix to that module's API. The path and query string SHALL arrive unchanged.

#### Scenario: Users request reaches the Users API
- **WHEN** a client sends `POST <gateway>/users/tokens/refresh?x=1`
- **THEN** the Users API receives `POST /users/tokens/refresh?x=1`

#### Scenario: Games request reaches the Games API
- **WHEN** a client sends `GET <gateway>/games/anything`
- **THEN** the Games API receives `GET /games/anything`
