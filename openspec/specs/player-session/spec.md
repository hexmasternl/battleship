# player-session Specification

## Purpose
Defines how the frontend keeps the player's identity: where the access and refresh tokens are stored and for how long, how a returning visitor is recognised on the landing page, how a new visitor becomes a player through the shared player-name component, and when the visitor counts as a known player.

## Requirements

### Requirement: Refresh tokens persist across browser sessions
The frontend SHALL store the refresh token and its expiry instant in `localStorage`, so that the player's identity survives closing and reopening the browser.

#### Scenario: Refresh token after a browser restart
- **WHEN** a player has received tokens, closes the browser and opens the application again
- **THEN** the refresh token received earlier is still available to the application

### Requirement: Access tokens last only for the browser session
The frontend SHALL store the access token and its expiry instant in `sessionStorage` and SHALL NOT write the access token to `localStorage`.

#### Scenario: Access token location
- **WHEN** the frontend has stored a token pair
- **THEN** `sessionStorage` holds the access token and its expiry, and no `localStorage` entry contains the access token

#### Scenario: Access token after the browser session ends
- **WHEN** the browser session in which the access token was stored has ended and the application is opened again
- **THEN** no access token is available until a new one is obtained

### Requirement: The landing page restores a returning player
When the landing page is opened and a refresh token is stored, the frontend SHALL send that refresh token to the Players service refresh endpoint and SHALL store the returned access and refresh tokens, replacing the previous ones. When no refresh token is stored, it SHALL NOT call the refresh endpoint.

#### Scenario: Returning player is refreshed
- **WHEN** the landing page is opened and `localStorage` holds a refresh token that has not expired
- **THEN** one refresh request carrying that token is sent, the new refresh token replaces the old one in `localStorage`, the new access token is stored in `sessionStorage`, and the player is known

#### Scenario: First visit
- **WHEN** the landing page is opened and no refresh token is stored
- **THEN** no refresh request is sent and the player is unknown

#### Scenario: Refresh is refused
- **WHEN** the refresh request for the stored refresh token is answered with `401 Unauthorized`
- **THEN** the stored refresh token and access token are removed and the player is unknown

#### Scenario: Refresh token expired locally
- **WHEN** the landing page is opened and the stored refresh token's expiry instant has passed
- **THEN** no refresh request is sent, the stored tokens are removed and the player is unknown

#### Scenario: Backend unreachable
- **WHEN** the refresh request fails because of a network error or a server error
- **THEN** the stored refresh token is kept, the player is unknown for now, and a later attempt to start a game tries the refresh again before asking for a player name

#### Scenario: Another tab rotated the token first
- **WHEN** a refresh is refused but `localStorage` now holds a different refresh token than the one that was sent
- **THEN** the stored tokens are not removed and the refresh is retried once with the stored token

#### Scenario: Landing page does not wait for the refresh
- **WHEN** the landing page is opened while a refresh request is pending
- **THEN** the hero and its calls to action are rendered without waiting for the response

### Requirement: A shared component asks for a player name and creates the player
The frontend SHALL provide a shared player-name component that asks the visitor for a player name, sends it to the anonymous Players service create-tokens endpoint, stores the returned access and refresh tokens as the other requirements describe, and signals its host when the player has been created.

#### Scenario: Player is created
- **WHEN** the visitor enters `Admiral Nelson` and submits the player-name component
- **THEN** one create-tokens request with the display name `Admiral Nelson` is sent without an `Authorization` header, the returned refresh token is stored in `localStorage`, the returned access token is stored in `sessionStorage`, the player is known and the component signals that the player was created

#### Scenario: Name is validated
- **WHEN** the visitor submits an empty name, a name of only spaces, or a name longer than 40 characters
- **THEN** no request is sent and the component shows a validation message linked to the name field

#### Scenario: Creation fails
- **WHEN** the create-tokens request fails
- **THEN** the component shows an error message, keeps the typed name, lets the visitor submit again, and stores no tokens

#### Scenario: No double submit
- **WHEN** a create-tokens request is pending
- **THEN** the submit control is disabled and a second submit sends no request

#### Scenario: Accessibility
- **WHEN** the player-name component is checked with axe
- **THEN** it reports no violations, and the name field has a visible label

### Requirement: A player is known when the frontend holds their tokens
The frontend SHALL treat the visitor as a known player once tokens were obtained from a successful refresh or from creating a player in the current page load, and as unknown otherwise.

#### Scenario: Known after creation
- **WHEN** the player-name component has created a player
- **THEN** the frontend reports the player as known without another request

#### Scenario: Unknown after a refused refresh
- **WHEN** the landing page refresh was refused
- **THEN** the frontend reports the player as unknown
