# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: The application opens one event stream for the current player`
- TO: `### Requirement: The application opens one event stream for the current user`

## MODIFIED Requirements

### Requirement: The application opens one event stream for the current user
The frontend SHALL provide a single application-wide events service that opens the server's event stream for the current user identity on request and keeps at most one stream open per application instance. Opening the stream again while it is open SHALL NOT create a second connection.

#### Scenario: First connect opens the stream
- **WHEN** the application asks the events service to connect for user A
- **THEN** exactly one stream to the server's events endpoint is opened carrying A's user id as the `userId` query parameter

#### Scenario: Repeated connect is idempotent
- **WHEN** the stream is already open and the application asks the events service to connect again for the same user
- **THEN** no second stream is opened and the existing one stays in use
