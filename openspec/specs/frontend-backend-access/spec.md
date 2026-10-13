# frontend-backend-access Specification

## Purpose

Defines how the frontend finds and calls the backend: the gateway URL arrives as runtime configuration and backend calls written as `/api/...` are sent to that gateway.

## Requirements

### Requirement: The frontend runs as an Aspire resource that knows the gateway URL
The AppHost SHALL start the Angular app as a resource that waits for the gateway and receives the gateway's HTTP endpoint in an environment variable.

#### Scenario: Frontend starts after the gateway
- **WHEN** the AppHost is started locally
- **THEN** the dashboard shows the frontend resource waiting for `gateway`, then running with the gateway endpoint in its environment

### Requirement: The gateway URL is loaded as runtime configuration
When Aspire starts the frontend, the app SHALL get the gateway URL from a runtime configuration file generated from the environment variable. It SHALL load that file before the first backend request. The URL SHALL NOT be compiled into the bundle.

#### Scenario: Configuration reflects the current run
- **WHEN** the gateway starts on a different port than on a previous run
- **THEN** the frontend sends backend requests to the new port without any file in the repository being edited

#### Scenario: Missing configuration is reported
- **WHEN** the app starts and the runtime configuration file is missing or has no gateway URL
- **THEN** the app logs a clear error naming the missing setting, and requests that start with `/api/` fail instead of being sent to an unintended host

### Requirement: Requests under /api are sent to the gateway
The frontend SHALL rewrite every HTTP request whose path starts with `/api/` to the gateway base URL followed by the rest of the path, with `/api` stripped and the query string kept. Other requests SHALL pass through unchanged.

#### Scenario: Players call is rewritten
- **WHEN** the app sends `GET /api/players/me?x=1`
- **THEN** the request goes to `<gateway>/players/me?x=1`

#### Scenario: Non-api request is untouched
- **WHEN** the app requests `/config.json` or an absolute URL that does not start with `/api/`
- **THEN** the request is sent unchanged

#### Scenario: App route is not an HTTP request
- **WHEN** the user navigates to the app route `/games`
- **THEN** the Angular router handles it and no request reaches the gateway
