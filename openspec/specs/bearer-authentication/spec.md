# bearer-authentication Specification

## Purpose
Defines how any Battleship API validates an access token issued by the Players service and exposes the caller's identity to its endpoints, so every service protects endpoints the same way from the same configuration.

## Requirements

### Requirement: Services validate Users access tokens with one shared registration
Every Battleship API SHALL be able to enable bearer authentication through a single shared registration from the service defaults, which validates the token signature, issuer, audience and expiry against the configured values.

#### Scenario: Valid token is accepted
- **WHEN** a request carries a bearer access token issued by the Users service and the API has the shared authentication enabled
- **THEN** the request is authenticated, the caller's user identifier is available as the subject claim and the display name claim is available to the endpoint

#### Scenario: Expired token is rejected
- **WHEN** a request to an endpoint that requires authorization carries an access token past its expiry
- **THEN** the response status is `401 Unauthorized` and the endpoint handler does not run

#### Scenario: Token with a different signing key is rejected
- **WHEN** a request carries a token signed with a key other than the configured one
- **THEN** the response status is `401 Unauthorized`

#### Scenario: Missing token on a protected endpoint
- **WHEN** a request without an `Authorization` header reaches an endpoint that requires authorization
- **THEN** the response status is `401 Unauthorized` and the response carries a `WWW-Authenticate: Bearer` header

### Requirement: Authentication configuration is shared and validated
The shared registration SHALL read the signing key, issuer and audience from the `Users:Tokens` configuration section, the same section the Users service uses to sign tokens, and SHALL fail at startup when the signing key is missing or shorter than 32 bytes.

#### Scenario: Service starts without a signing key
- **WHEN** an API enables the shared authentication without a configured signing key
- **THEN** startup fails with an error naming the missing configuration key `Users:Tokens:SigningKey`

#### Scenario: Clock skew is bounded
- **WHEN** a token expired 10 seconds ago is presented
- **THEN** it is rejected, because the accepted clock skew is at most 30 seconds and the configured value is applied consistently across services

#### Scenario: Token from the old issuer is refused
- **WHEN** a request carries an access token whose issuer is `battleship-players`
- **THEN** the response status is `401 Unauthorized`

### Requirement: Endpoints opt in to authorization explicitly
Enabling the shared authentication SHALL NOT protect endpoints by itself; an endpoint or route group is protected only when it requires authorization, so existing anonymous endpoints keep working.

#### Scenario: Anonymous endpoint on a service with authentication enabled
- **WHEN** a request without a token reaches an endpoint that does not require authorization on a service that has the shared authentication enabled
- **THEN** the request is handled normally with a `2xx` response
