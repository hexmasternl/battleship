# Proposal

> Tracks GitHub issue [hexmasternl/battleship#16](https://github.com/hexmasternl/battleship/issues/16).

## Why

After #13 a player can create a game, but nobody can take the second seat: "Join an existing game" still leads to a coming-soon panel and the Games API has no way to add player two. Joining is the step that turns a lonely game into a match and the entry point to the placement phase (#17). It is also the first change in which two players write to the same game, so the game's storage needs protection against concurrent updates before #17, #18 and #19 add more writers.

## What Changes

- **Join a game.** A new authenticated endpoint, `POST /games/{code}/join` (`/api/games/{code}/join` through the gateway). It takes no body; the caller's player id and name come from the access token (`sub`, `name`), as in #13.
  - Unknown code: `404 Not Found`.
  - Caller is already player one or player two: `200 OK` with the game, no change and no event. Re-joining after a refresh or a second click is safe.
  - Game is not `New` (started, completed, abandoned): `409 Conflict` problem details of type "game already started".
  - Player two is taken by someone else: `409 Conflict` problem details of type "game full".
  - Otherwise the caller becomes player two, the game stays `New`, and the API answers `200 OK` with `{ gameId, gameCode }`.
- **Game joined event.** A successful join publishes `game.joined.v1` (`GameJoinedEvent`, already in the catalog) to both players of the game. A publish failure is logged and swallowed, as in #13.
- **Domain.** `Game.Join(playerId, playerName, now)` adds player two and refuses with a domain exception when the game is not `New` or player two is taken. Activity is touched for the game and the joining player.
- **Optimistic concurrency.** `IGameRepository` gains an update that writes with the stored ETag and reports a conflict instead of overwriting. A shared game updater loads, mutates and saves a game, and reloads and retries up to 3 times on a conflict. This change uses it for join; #17, #18 and #19 reuse it as is.
- **Frontend.**
  - "Join an existing game" becomes a button. After `ensurePlayer()` (#7's player-name dialog for an unknown player) it opens a `JoinGameDialog`.
  - The dialog has a game code input (8 characters `A-Z0-9`, upper-cased while typing), a join action with a progress indicator and a double-submit guard, and inline errors for "game not found", "game full" and "game already started" with a way to try again.
  - On success the dialog closes and the app navigates to `/games/{code}`, the page #13 created and #17 builds.
  - `GamesApi` gains `joinGame(code)`.
  - The `/games/join` coming-soon route is removed.

## Capabilities

### New Capabilities
- `game-joining`: how a known player joins an existing game as player two. Covers authentication, identity from the token, the code lookup, the refusals (not found, full, started), idempotent re-join, the response and the game joined event.

### Modified Capabilities
- `game-state`: gains a requirement that concurrent updates to one game never overwrite each other. The requirement is ADDED to the `game-state` capability that `issue-13-create-new-game` introduces.
- `welcome-page`: "Calls to action navigate to the game routes" changes. "Join an existing game" opens the join dialog for a known player and navigates to `/games/{game-code}` after a successful join, instead of navigating to `/games/join`. The delta is based on the version of this requirement that `issue-13-create-new-game` introduces.

## Impact

- **Games module** (`src/Games/`):
  - `Games.Abstractions`: `JoinGameResponse`, the repository update contract (`TryUpdateAsync` with a concurrency token).
  - `Games`: `Game.Join`, the join refusal exception, the `JoinGame` feature slice, the shared `GameUpdater` with bounded retry, and the join endpoint in `GamesEndpoints`.
  - `Games.Data`: `GameTableRepository` reads the ETag and writes updates with `UpdateEntityAsync(..., ifMatch)`.
  - `Games.Tests`: domain, updater, handler, repository and endpoint tests.
- **Gateway**: the `games` prefix from #11 already covers `/api/games/{code}/join`. No route change.
- **Integration events**: none. `GameJoinedEvent` and `EventAudience.Game(...)` already exist in `src/Core/HexMaster.Battleship.IntegrationEvents`.
- **Frontend** (`src/Frontend/src/app/`), built by the `angular-frontend` agent:
  - `games/` gains `joinGame` on `GamesApi` and `join-game-dialog/` with its component-scoped store.
  - `welcome/hero-section` turns "Join an existing game" into a button that opens the dialog.
  - `app.routes.ts` loses `games/join`.
- **Depends on**: `issue-13-create-new-game` (and transitively #7, #11, #14), which must be applied and archived first. The `game-state` and `welcome-page` deltas build on #13's versions.
- **Followed by**: #17 (placement phase, `GET /games/{code}`, readiness), which reuses the optimistic concurrency of this change.
- **Assumed**:
  - The issue's redirect target `/game/{game-code}` is a typo for #13's route `/games/{game-code}`.
  - "Phase one of the game starts" means the joined game stays `New` and its page shows the placement phase (#17). Joining does not start the game.
  - Joining your own game (you are player one) is a harmless no-op that returns `200`, so the creator can use the dialog to get back to their game.
- **Out of scope**: listing open games, invitations or links that join directly, leaving a game, and the game page itself (#17).
