# user-identification Specification

## Purpose
Defines how an anonymous visitor becomes a uniquely identified user of Battleship: how tokens are issued without credentials, what the access and refresh tokens guarantee, how a refresh token is exchanged and when it is refused, and what the system remembers about a user.

## Requirements

### Requirement: An anonymous request creates a user and issues tokens
The Players service SHALL accept an unauthenticated request to create tokens, create a user with a unique identifier and a display name, persist that user, and respond with an access token, a refresh token, and the expiry instant of each.

#### Scenario: Tokens are created with a display name
- **WHEN** an unauthenticated client posts a create-tokens request with the display name `Admiral Nelson`
- **THEN** the response status is `201 Created`, the body contains a non-empty access token, a non-empty refresh token, an access token expiry in the future and a refresh token expiry later than the access token expiry, and a user with display name `Admiral Nelson` exists in the data store

#### Scenario: Tokens are created without a display name
- **WHEN** an unauthenticated client posts a create-tokens request with no display name
- **THEN** the response status is `201 Created` and the created user has a generated, non-empty display name

#### Scenario: Two requests create two users
- **WHEN** two create-tokens requests are made
- **THEN** the two users have different identifiers and the two refresh tokens are different

#### Scenario: Display name is validated
- **WHEN** a create-tokens request carries a display name that is blank or longer than 40 characters
- **THEN** the response status is `400 Bad Request` with a problem-details body and no user is created

### Requirement: Access tokens are signed, short-lived and carry the user identity
Every access token SHALL be a JSON Web Token signed by the Players service, SHALL carry the user identifier as its subject and the display name as a claim, and SHALL expire after the configured access token lifetime, which defaults to 15 minutes.

#### Scenario: Access token claims
- **WHEN** an issued access token is decoded
- **THEN** its subject equals the user's identifier, it carries the user's display name, its issuer and audience equal the configured values, and its expiry is the issue time plus the configured access token lifetime

#### Scenario: Tampered access token is rejected
- **WHEN** a client presents an access token whose payload was altered after signing
- **THEN** signature validation fails and the token is treated as absent

### Requirement: Refresh tokens are opaque, hashed at rest and expire after 90 days
Every refresh token SHALL be an unpredictable random value of at least 256 bits. The service SHALL store only a one-way hash of it, never the value itself, and SHALL treat it as expired 90 days after it was issued.

#### Scenario: Refresh token at rest
- **WHEN** the refresh token table is inspected after tokens were issued
- **THEN** no row contains the refresh token value that was returned to the client, and each row carries an expiry 90 days after its issue time

#### Scenario: Refresh token after 90 days
- **WHEN** a client presents a refresh token issued more than 90 days ago
- **THEN** the response status is `401 Unauthorized` and no tokens are issued

### Requirement: A valid refresh token is exchanged for a new token pair with rotation
The Players service SHALL accept a refresh request carrying a refresh token and, when the token is known, unexpired and not yet used, and the user exists and is not revoked, SHALL respond with a new access token and a new refresh token, invalidate the presented refresh token, and record the user's last-seen time.

#### Scenario: Successful refresh
- **WHEN** a client posts a refresh request with a refresh token issued moments ago
- **THEN** the response status is `200 OK`, the body contains a new access token whose subject is the same user identifier, a new refresh token different from the presented one, and both expiry instants; and the user's last-seen time is updated

#### Scenario: Presented refresh token cannot be reused
- **WHEN** a client posts a refresh request with a refresh token that was already exchanged
- **THEN** the response status is `401 Unauthorized` with a problem-details body and no tokens are issued

#### Scenario: Unknown refresh token
- **WHEN** a client posts a refresh request with a value that was never issued
- **THEN** the response status is `401 Unauthorized` with a problem-details body

#### Scenario: Revoked user
- **WHEN** a client posts a refresh request with a valid refresh token of a user whose record is marked revoked
- **THEN** the response status is `401 Unauthorized` and no tokens are issued

#### Scenario: Malformed refresh request
- **WHEN** a client posts a refresh request with an empty refresh token
- **THEN** the response status is `400 Bad Request` with a problem-details body

### Requirement: The system keeps a record of every known user
For every user the Players service SHALL persist the unique identifier, the display name, the creation time, the last-seen time and a revoked flag, and SHALL be able to find the user by identifier.

#### Scenario: User record after creation
- **WHEN** tokens were created for a new user
- **THEN** the user record holds the identifier that is the access token's subject, the display name, a creation time equal to the issue time, a last-seen time equal to the creation time, and the revoked flag cleared

### Requirement: A user can read their own identity
The Players service SHALL expose an endpoint that requires a valid access token and returns the identifier and display name of the user the token belongs to.

#### Scenario: Authenticated identity read
- **WHEN** a client calls the current-user endpoint with a valid access token
- **THEN** the response status is `200 OK` and the body contains the user's identifier and display name

#### Scenario: Unauthenticated identity read
- **WHEN** a client calls the current-user endpoint without an access token, or with an expired one
- **THEN** the response status is `401 Unauthorized`

### Requirement: Token configuration is validated at startup
The Players service SHALL read the signing key, issuer, audience and token lifetimes from configuration and SHALL refuse to start when the signing key is missing or shorter than 32 bytes.

#### Scenario: Missing signing key
- **WHEN** the Players API starts without a configured signing key
- **THEN** startup fails with an error naming the missing configuration key, and the service does not listen for requests

#### Scenario: Configured lifetimes are applied
- **WHEN** the access token lifetime is configured as 5 minutes and tokens are created
- **THEN** the access token expiry is 5 minutes after issue and the refresh token expiry remains 90 days after issue

### Requirement: Identity data is stored in Azure Table Storage and runs locally on the emulator
The Players service SHALL persist users and refresh tokens in Azure Table Storage, and the local development environment SHALL provide that storage through the Azurite emulator without any Azure account.

#### Scenario: Local run without Azure
- **WHEN** the Aspire AppHost is started on a machine with a container runtime and no Azure credentials
- **THEN** an Azurite resource starts, the Players API is healthy, and creating tokens succeeds and leaves a user row in the emulator

#### Scenario: Tables are created on demand
- **WHEN** the Players API handles its first request against an empty storage account
- **THEN** the users and refresh token tables exist afterwards without manual provisioning
