# Spec Delta

## REMOVED Requirements

### Requirement: Requests under /api are sent to the gateway
**Reason**: Its example used `/api/players/me`, a path no service serves; the identity service is the Users service.
**Migration**: Replaced by "Requests under /api are forwarded to the gateway", identical except for a real Users path in the example.

## ADDED Requirements

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
