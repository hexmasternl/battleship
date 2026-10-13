# Spec Delta

## MODIFIED Requirements

### Requirement: Internal endpoints are not exposed through the gateway
Endpoints meant only for the platform, for diagnostics or for scheduled maintenance, such as Dapr subscription routes, smoke endpoints and endpoints under `/maintenance`, SHALL NOT be reachable through the gateway. The prefixes of such endpoints SHALL be reserved, so the route table cannot declare them for any module.

#### Scenario: Smoke endpoint is not reachable through the gateway
- **WHEN** a client sends `POST <gateway>/smoke/publish`
- **THEN** the gateway answers `404 Not Found` and the Players API does not receive the request

#### Scenario: Maintenance endpoint is not reachable through the gateway
- **WHEN** a client sends `POST <gateway>/maintenance/game-cleanup`
- **THEN** the gateway answers `404 Not Found` and the Games API does not receive the request

#### Scenario: Reserved prefix cannot be declared
- **WHEN** the route table declares `maintenance` as a prefix of any module
- **THEN** the application host refuses to start and names the module and the reserved prefix
