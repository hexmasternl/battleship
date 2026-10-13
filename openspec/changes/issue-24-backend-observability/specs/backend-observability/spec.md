# Spec Delta

## Purpose

Defines the telemetry every backend service emits: traces for use cases and the integration event pipeline, business metrics, and structured logs correlated with traces. Together they let a developer follow and measure any backend behaviour from the Aspire dashboard without reading code.

## ADDED Requirements

### Requirement: Every use case is traced
Every command and query a backend service handles SHALL run inside its own span, named after the use case and a child of the inbound request's span. The span SHALL end with status OK when the use case succeeds. When it fails, the span SHALL end with status Error, carry the error message and record the exception. A handler added later SHALL get this span without code of its own.

#### Scenario: Successful use case
- **WHEN** a client creates a token pair through the gateway
- **THEN** the trace on the dashboard shows the gateway span, the Players API request span, and beneath it a use-case span for creating tokens with status OK, followed by the table storage spans it caused

#### Scenario: Failing use case
- **WHEN** a client presents an unknown refresh token
- **THEN** the use-case span for refreshing tokens ends with status Error and records the exception, and the response is still 401

#### Scenario: New handler is covered automatically
- **WHEN** a new command handler is registered in any module
- **THEN** its executions appear as use-case spans without the handler creating a span itself

### Requirement: Spans carry low-cardinality domain attributes
Use-case and pipeline spans SHALL carry the domain attributes needed for troubleshooting, prefixed with their domain (`player.`, `game.`, `event.`, `connection.`). Where an attribute name exists in the OpenTelemetry semantic conventions, that name SHALL be used. Internal player and game identifiers MAY appear on spans. Tokens, secrets and display names SHALL NOT.

#### Scenario: Refused refresh is explained
- **WHEN** a refresh is refused because the token was used already
- **THEN** the use-case span carries the refusal reason as an attribute, and no token value appears anywhere in the span

### Requirement: One trace follows an integration event end to end
Publishing an integration event SHALL create a producer span. Its trace context SHALL travel with the event through the pub/sub component, so the Events service's handling of that event and the deliveries to connected players appear in the same trace as the publishing request.

#### Scenario: Event reaches the browser in one trace
- **WHEN** a use case publishes an integration event that targets a connected player
- **THEN** the dashboard shows one trace containing the publisher's use-case span, the producer span, the sidecar spans, the Events service's receive span, and a delivery span for the targeted connection

#### Scenario: Dropped envelope is visible
- **WHEN** the Events service receives an envelope that fails validation
- **THEN** its receive span ends with status Error and the reason is recorded, and the drop is counted in metrics

### Requirement: Dapr sidecars report to the dashboard
Every Dapr sidecar the application host starts SHALL export its spans to the same telemetry endpoint as the services. Sidecar spans SHALL appear in the dashboard traces of the requests that caused them.

#### Scenario: Sidecar spans are visible
- **WHEN** a service publishes through its sidecar during a local run
- **THEN** the trace on the dashboard includes spans emitted by that sidecar

### Requirement: Business operations emit metrics
Each backend module SHALL emit metrics for its business operations, named `<module>.<thing>` with a unit and a description. At least:
- Players: tokens issued, refreshed and refused (refusals by reason)
- integration events: published and failed (by event type)
- Events: active connections, and events delivered, dropped (by reason) and replayed
- every use case: duration, by use case and outcome

#### Scenario: Token metrics move
- **WHEN** a new visitor gets a token pair and later refreshes it once
- **THEN** the dashboard shows the tokens-issued counter increased by one and the tokens-refreshed counter increased by one

#### Scenario: Connection gauge follows the streams
- **WHEN** two browsers open the event stream and one of them closes it
- **THEN** the active-connections metric reads two and then one

### Requirement: Metric dimensions stay low-cardinality
Metric tags SHALL be drawn from bounded sets such as use-case name, outcome, event type and refusal reason. Metric tags SHALL NOT contain player ids, game ids, game codes, tokens or any other per-entity or personal value.

#### Scenario: No per-player series
- **WHEN** a hundred different players obtain tokens
- **THEN** the tokens-issued metric still has a single series per outcome, not one per player

### Requirement: Logs are structured and correlated
Every log entry a backend service writes SHALL be structured: a fixed message template with named values, kept as separate fields in the exported record. Entries written while a span is active SHALL carry that span's trace and span ids. Each module SHALL define its log messages once, with a stable event id per message.

#### Scenario: Log entry links to its trace
- **WHEN** the Events service logs that it routed an event
- **THEN** the dashboard shows the entry with its named values as separate fields and links it to the trace of the request that published the event

### Requirement: Telemetry never leaks secrets or personal data
Logs, span attributes and metric tags SHALL NOT contain access tokens, refresh tokens, signing keys, passwords, connection strings or display names.

#### Scenario: Token flow is clean
- **WHEN** the full telemetry of a token creation and a refresh is exported
- **THEN** no exported log, span attribute or metric tag contains a token value, the signing key or the visitor's display name

### Requirement: Export stays vendor-neutral
Backend services SHALL export telemetry only through the OpenTelemetry protocol, with the endpoint, service name and resource attributes taken from the standard `OTEL_*` settings. No vendor-specific telemetry SDK SHALL be referenced by service code.

#### Scenario: Local run exports to the dashboard
- **WHEN** the application runs under the Aspire AppHost
- **THEN** every backend service appears in the dashboard's traces, metrics and structured logs under its resource name

#### Scenario: No exporter without an endpoint
- **WHEN** a service runs without an OTLP endpoint configured
- **THEN** it starts and serves requests normally without trying to export
