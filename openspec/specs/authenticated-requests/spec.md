# authenticated-requests Specification

## Purpose
Defines how the frontend authenticates its calls to the backend: which requests carry the player's access token, how an expired access token is renewed before a request is sent, and how a refused token is recovered from without bothering the player.

## Requirements

### Requirement: Backend requests carry the access token
The frontend SHALL add an `Authorization: Bearer <access token>` header to every request to the backend (`/api/...`) while the player session holds an access token. Token issue and refresh requests, and requests that are not for the backend, SHALL NOT carry the header.

#### Scenario: Game request is authenticated
- **WHEN** a known player's frontend sends `POST /api/games`
- **THEN** the request carries the player's current access token as a bearer token

#### Scenario: Token refresh is not authenticated
- **WHEN** the frontend sends the refresh-token request
- **THEN** that request has no `Authorization` header

#### Scenario: Other hosts are left alone
- **WHEN** the frontend requests a resource that is not under `/api/`
- **THEN** no `Authorization` header is added

### Requirement: An expired access token is renewed before sending
When the stored access token has expired or will expire within thirty seconds, and a refresh token is stored, the frontend SHALL renew the token pair through the player session before sending a backend request, and SHALL send the request with the new access token. Concurrent requests SHALL share one renewal.

#### Scenario: Renewal before the request
- **WHEN** the access token has expired and the player creates a game
- **THEN** one refresh request is sent first and the create request carries the new access token

#### Scenario: Two requests share one renewal
- **WHEN** two backend requests are sent while the access token is expired
- **THEN** exactly one refresh request is made and both requests carry the new access token

### Requirement: A refused token is retried once
When a backend request that carried an access token is answered with `401 Unauthorized`, the frontend SHALL renew the token pair once and repeat the request once. If renewal fails or the repeated request is refused again, the error SHALL be passed to the caller and the session SHALL follow the player session's rules for a refused refresh.

#### Scenario: Retry succeeds
- **WHEN** `POST /api/games` returns `401` and the refresh succeeds
- **THEN** the request is repeated once with the new access token and its result is returned to the caller

#### Scenario: Retry is not repeated
- **WHEN** the repeated request returns `401` again
- **THEN** no further refresh or repeat is attempted and the caller receives the `401` error
