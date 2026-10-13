# Spec Delta

## MODIFIED Requirements

### Requirement: Every use case is traced
Every command and query a backend service handles SHALL run inside its own span, named after the use case and a child of the inbound request's span. The span SHALL end with status OK when the use case succeeds. When it fails, the span SHALL end with status Error, carry the error message and record the exception. A handler added later SHALL get this span without code of its own.

#### Scenario: Successful use case
- **WHEN** a client creates a token pair through the gateway
- **THEN** the trace on the dashboard shows the gateway span, the Users API request span, and beneath it a use-case span for creating tokens with status OK, followed by the table storage spans it caused

#### Scenario: Failing use case
- **WHEN** a client presents an unknown refresh token
- **THEN** the use-case span for refreshing tokens ends with status Error and records the exception, and the response is still 401

#### Scenario: New handler is covered automatically
- **WHEN** a new command handler is registered in any module
- **THEN** its executions appear as use-case spans without the handler creating a span itself

### Requirement: Spans carry low-cardinality domain attributes
Use-case and pipeline spans SHALL carry the domain attributes needed for troubleshooting, prefixed with their domain (`user.` in the Users module, `player.` and `game.` in the Games module, `event.`, `connection.`). Where an attribute name exists in the OpenTelemetry semantic conventions, that name SHALL be used. Internal user, player and game identifiers MAY appear on spans. Tokens, secrets and display names SHALL NOT.

#### Scenario: Refused refresh is explained
- **WHEN** a refresh is refused because the token was used already
- **THEN** the use-case span carries the refusal reason as an attribute, and no token value appears anywhere in the span

#### Scenario: Token span names the user
- **WHEN** a client creates a token pair
- **THEN** the use-case span for creating tokens carries the new user's internal id as `user.id`

### Requirement: One trace follows an integration event end to end
Publishing an integration event SHALL create a producer span. Its trace context SHALL travel with the event through the pub/sub component, so the Events service's handling of that event and the deliveries to connected users appear in the same trace as the publishing request.

#### Scenario: Event reaches the browser in one trace
- **WHEN** a use case publishes an integration event that targets a connected user
- **THEN** the dashboard shows one trace containing the publisher's use-case span, the producer span, the sidecar spans, the Events service's receive span, and a delivery span for the targeted connection

#### Scenario: Dropped envelope is visible
- **WHEN** the Events service receives an envelope that fails validation
- **THEN** its receive span ends with status Error and the reason is recorded, and the drop is counted in metrics

### Requirement: Business operations emit metrics
Each backend module SHALL emit metrics for its business operations, named `<module>.<thing>` with a unit and a description. At least:
- Users: tokens issued, refreshed and refused (refusals by reason), as `users.tokens.issued`, `users.tokens.refreshed` and `users.tokens.refused`
- integration events: published and failed (by event type)
- Events: active connections, and events delivered, dropped (by reason) and replayed
- every use case: duration, by use case and outcome

#### Scenario: Token metrics move
- **WHEN** a new visitor gets a token pair and later refreshes it once
- **THEN** the dashboard shows the tokens-issued counter of the `HexMaster.Battleship.Users` meter increased by one and the tokens-refreshed counter increased by one

#### Scenario: Connection gauge follows the streams
- **WHEN** two browsers open the event stream and one of them closes it
- **THEN** the active-connections metric reads two and then one

## REMOVED Requirements

### Requirement: Metric dimensions stay low-cardinality
**Reason**: Its scenario was named after players obtaining tokens; tokens are issued to users, and a MODIFIED block cannot rename a scenario.
**Migration**: Replaced by "Metric dimensions stay bounded", identical except that it names user ids next to player ids and its scenario speaks of users.

## ADDED Requirements

### Requirement: Metric dimensions stay bounded
Metric tags SHALL be drawn from bounded sets such as use-case name, outcome, event type and refusal reason. Metric tags SHALL NOT contain user ids, player ids, game ids, game codes, tokens or any other per-entity or personal value.

#### Scenario: No per-user series
- **WHEN** a hundred different users obtain tokens
- **THEN** the tokens-issued metric still has a single series per outcome, not one per user
