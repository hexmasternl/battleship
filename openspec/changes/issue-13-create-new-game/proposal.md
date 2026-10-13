# Proposal

> Tracks GitHub issue [hexmasternl/battleship#13](https://github.com/hexmasternl/battleship/issues/13).

## Why

Players cannot start a game yet. The landing page's "Create a new game" leads to a coming-soon panel, and the Games module is still an empty template. Creating a game is the first step of every match and the entry point for the placement phase (#17). It also gives the game domain its foundation: the game model, its states, its two players and the turn rule.

## What Changes

- **Game model.** A `Game` aggregate in the Games module:
  - a Guid id;
  - a unique 8-character game code made of `A-Z` and `0-9`;
  - a state (`New`, `Started`, `Completed`, `Abandoned`);
  - `PlayerOne` and `PlayerTwo`;
  - a `LastActivityOn` that every change to the game sets to UTC now.

  Each player (`IPlayer`) has `PlayerId`, `PlayerName`, `HasTurn`, `ShipCoordinates`, `ShotCoordinates` and `LastActivityOn`. The aggregate enforces the turn rule: at most one player has the turn, and a switch flips both players together.
- **Create a game.** A new authenticated endpoint, `POST /games` (`/api/games` through the gateway). It takes the caller's player id and name from the access token and creates a `New` game:
  - the caller is `PlayerOne`;
  - `PlayerTwo` is empty;
  - neither player has the turn.

  It stores the game, publishes `GameCreatedEvent` (`game.created.v1`) to the creator, and returns `201 Created` with the game id and code.
- **Storage.** A `Games.Data` project persists games in Azure Table Storage (Azurite locally, through Aspire). The game state is serialized as one JSON object per game, and the game code is the key, which guarantees uniqueness.
- **AppHost.** The Games API gets the table storage resource, the shared signing key and bearer validation from #7.
- **Frontend.**
  - "Create a new game" first ensures the player is known (#7's player-name dialog for an unknown player). It then opens a confirmation dialog. Confirming calls the Games API and redirects to `/games/{game-code}`, which shows a placeholder until #17 builds the page.
  - The `/games/new` placeholder route is removed.
- **Frontend authenticated requests.** An HTTP interceptor attaches the access token to backend requests. It renews an expired access token through the player session before sending, and it retries once after a `401`. #7 explicitly left this to the first game feature.

## Capabilities

### New Capabilities
- `game-state`: the game aggregate. Covers its identity and code, lifecycle states, the two players and their data, the turn rule, activity time tracking, and how a game's state is persisted.
- `game-creation`: how a known player creates a game. Covers authentication, the initial state, uniqueness of the code, the response, and the game created event.
- `authenticated-requests`: how the frontend authenticates its backend calls. Covers attaching the access token, renewal before expiry, and the single retry after a refused token.

### Modified Capabilities
- `welcome-page`: "Calls to action navigate to the game routes" changes. "Create a new game" opens a confirmation dialog for a known player, creates the game, and navigates to `/games/{game-code}` instead of `/games/new`. The delta is based on the version of this requirement that `issue-7-user-identification` introduces.

## Impact

- **Games module** (`src/Games/`):
  - `HexMaster.Battleship.Games.Abstractions`: DTOs, `IGame` and `IPlayer`, the repository interface.
  - `HexMaster.Battleship.Games`: domain models, the `CreateGame` feature slice and endpoints.
  - New `HexMaster.Battleship.Games.Data`: table entity and repository.
  - `HexMaster.Battleship.Games.Api`: wiring. The template `/weatherforecast` endpoint is removed.
  - `HexMaster.Battleship.Games.Tests`: Moq and Bogus are added.
- **AppHost**:
  - The Games API references the `tables` resource and the `players-signing-key` parameter that #7 introduces.
  - The `games` gateway prefix from #11 is used unchanged.
- **Frontend** (`src/Frontend/src/app/`):
  - `games/` gains the create-game API client, dialog and store.
  - `core/session/` gains the authentication interceptor.
  - The `welcome/hero-section` call to action changes.
  - In `app.routes.ts`, `games/:gameCode` replaces `games/new`.
- **Dependencies**: `Aspire.Azure.Data.Tables` in `Games.Data` (the same package and version #7 adds to Players).
- **Depends on**, which must be applied first:
  - `issue-7-user-identification`: Core base types, `AddBattleshipAuthentication`, the player session and the storage resource.
  - `issue-11-yarp-local-gateway`: `provideHttpClient`, the `/api` rewrite and the `games` route.
  - `issue-14-integration-events-catalog`: `GameCreatedEvent` and `IIntegrationEventSender`.

  #7 must also be archived before this change, because the `welcome-page` delta builds on its version of the requirement.
- **Followed by**: #17 (the `/games/{game-code}` page and the placement phase), and later the join, start, shoot and end flows.
- **Out of scope**:
  - Joining a game.
  - Ship placement and the game page itself.
  - Turns being given out (only the rule exists).
  - Cleaning up abandoned games.
  - Fetching a game by code (#17).
