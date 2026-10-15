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
The app SHALL get the gateway URL and the Events API URL from a runtime configuration file. When Aspire starts the frontend, the file SHALL be generated from environment variables; when the app is deployed, the deployment SHALL write it with the deployed URLs. The app SHALL load the file before the first backend request or event stream. Neither URL SHALL be compiled into the bundle.

#### Scenario: Configuration reflects the current run
- **WHEN** the gateway starts on a different port than on a previous run
- **THEN** the frontend sends backend requests to the new port without any file in the repository being edited

#### Scenario: Events stream uses the configured URL
- **WHEN** the runtime configuration names an Events API URL
- **THEN** the app opens the event stream at that URL

#### Scenario: Same bundle for every environment
- **WHEN** the same frontend build is served with a different runtime configuration file
- **THEN** it calls the backend and opens the event stream named in that file

#### Scenario: Missing configuration is reported
- **WHEN** the app starts and the runtime configuration file is missing or has no gateway URL
- **THEN** the app logs a clear error naming the missing setting, and requests that start with `/api/` fail instead of being sent to an unintended host

#### Scenario: Missing events URL is reported
- **WHEN** the runtime configuration has no Events API URL
- **THEN** the app logs a clear error naming the missing setting and does not open an event stream to an unintended host

### Requirement: Requests under /api are forwarded to the gateway
The frontend SHALL rewrite every HTTP request whose path starts with `/api/` to the gateway base URL followed by the rest of the path, with `/api` stripped and the query string kept. Other requests SHALL pass through unchanged.

#### Scenario: Users call is rewritten
- **WHEN** the app sends `GET /api/users/me?x=1`
- **THEN** the request goes to `<gateway>/users/me?x=1`

#### Scenario: Non-api request is untouched
- **WHEN** the app requests `/config.json` or an absolute URL that does not start with `/api/`
- **THEN** the request is sent unchanged

#### Scenario: App route is not an HTTP request
- **WHEN** the user navigates to the app route `/games`
- **THEN** the Angular router handles it and no request reaches the gateway
