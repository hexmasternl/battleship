# Spec Delta

## Purpose

Defines the interactive API reference every backend API serves during local development. A developer can discover, read and try each module's public endpoints from the Aspire dashboard without writing requests by hand.

## ADDED Requirements

### Requirement: Every API serves an interactive reference in Development
Each backend API SHALL serve an interactive API reference, built from its OpenAPI document, on its own endpoint when it runs in the Development environment. The reference SHALL list every public endpoint with its name, summary, parameters, request body and response types.

#### Scenario: Players reference lists the token endpoints
- **WHEN** a developer opens the Players API reference during a local run
- **THEN** it lists `POST /users/tokens`, `POST /users/tokens/refresh` and `GET /users/me`, each with its request and response shapes

#### Scenario: Every API has one
- **WHEN** the AppHost is running
- **THEN** the Players, Games and Events APIs each serve a reference page

### Requirement: The reference is unavailable outside Development
An API running in any environment other than Development SHALL serve neither the OpenAPI document nor the reference UI.

#### Scenario: Production does not expose it
- **WHEN** an API runs with the environment set to Production and the reference path is requested
- **THEN** the response is 404

### Requirement: Protected endpoints can be tried with a token
Each OpenAPI document SHALL describe the bearer-token scheme the APIs accept and mark which operations require it. From the reference, a developer SHALL be able to supply an access token once and call protected endpoints with it.

#### Scenario: Calling a protected endpoint
- **WHEN** a developer creates a token pair from the reference, enters the access token as the bearer credential and calls `GET /users/me`
- **THEN** the call succeeds with the caller's id and display name

#### Scenario: Anonymous endpoints stay anonymous
- **WHEN** the reference shows `POST /users/tokens`
- **THEN** that operation is not marked as requiring a token

### Requirement: Internal endpoints are not documented
The OpenAPI documents and references SHALL list only endpoints meant for clients. They SHALL omit platform and infrastructure endpoints such as Dapr subscription discovery, Dapr event delivery, health and liveness.

#### Scenario: Events reference hides the delivery endpoint
- **WHEN** a developer opens the Events API reference
- **THEN** it lists `GET /events` and does not list `POST /integration-events` or `/dapr/subscribe`

### Requirement: The dashboard links to each reference
On a local run, each backend API resource on the Aspire dashboard SHALL show a link labelled as its API reference that opens that API's reference page.

#### Scenario: Opening the reference from the dashboard
- **WHEN** a developer clicks the API reference link on the Games API resource
- **THEN** the Games API reference opens in the browser

### Requirement: The gateway never routes the reference
The paths of the OpenAPI document and the reference UI SHALL be reserved, so that no module can route them through the gateway.

#### Scenario: Reference path requested through the gateway
- **WHEN** the reference path is requested on the gateway endpoint
- **THEN** the gateway has no route for it, and a module declaring that path as its prefix makes the AppHost refuse to start
