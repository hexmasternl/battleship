# Proposal

> Tracks GitHub issue [hexmasternl/battleship#18](https://github.com/hexmasternl/battleship/issues/18).

## Why

After #17 both players can place their fleets and get the game to `Started`, but then nothing can happen: there is no way to fire a shot, the turn never moves, and the game page only shows a "game started" placeholder. Shooting in turns is the core of the game. It is also the last step before #19 can decide a winner.

## What Changes

- **Fire a shot.** A new authenticated endpoint, `POST /games/{code}/shots` (`/api/games/{code}/shots` through the gateway), with body `{ column, row }`:
  - the shooter is taken from the access token, as in #13, #16 and #17;
  - `404` when the code is unknown or the caller is not one of the players (same rule as #17's `GET /games/{code}`);
  - `400` when the cell is outside the 12x12 board;
  - `409` problem details when the game is not `Started`, the caller does not have the turn, or the caller already shot at that cell. Each refusal has a stable problem `type` the frontend maps to a message;
  - on success `200 OK` with `{ column, row, hit }`.
- **Domain.** `Game.FireShot(playerId, coordinate, now)` decides hit or miss against the opponent's ship cells, records `Shot(Column, Row, Hit)` on the shooter, and always calls `SwitchTurn`. The write uses #16's optimistic concurrency, so a double click or two racing requests cannot fire twice in one turn.
- **Integration events.** After a stored shot the Games service publishes, to both players and in this order:
  - `game.shot-fired.v1`, whose payload gains `hit` (additive, so the version stays `v1`);
  - a **new catalog event** `game.turn-changed.v1` (`GameTurnChangedEvent(GameId, GameCode, PlayerWithTurnId)`).

  Publish failures are logged and swallowed, as in #13. The frontend's `REALTIME_EVENT_TYPES` gains `game.turn-changed.v1`.
- **Game play page.** When the game is `Started`, #17's `GamePage` renders a `GamePlayPhase` instead of the placeholder:
  - Material tabs "You" (the opponent's waters as targeted by me: my shots, grey dot for a miss, red dot for a hit) and "Opponent" (my own board: my ships plus the opponent's shots);
  - the tabs switch automatically when the turn changes: my turn shows "You", the opponent's turn shows "Opponent";
  - with the turn, I tap or click an unshot cell; an interactive snackbar (Cancel / Shoot) confirms every shot, on every device;
  - the response adds the dot; opponent shots arrive over the event stream; every new dot animates in, without animation under `prefers-reduced-motion`;
  - a turn indicator says whose turn it is; a refresh at any moment restores the same view from `GET /games/{code}`.

## Capabilities

### New Capabilities
- `game-play`: how a started game is played on the server. Covers firing a shot, who may fire and where, hit detection, the recorded shot, the turn switch after every shot, and the events that announce both.
- `game-play-page`: the game page during the `Started` phase. Covers the two boards and their tabs, automatic tab switching on turn changes, selecting and confirming a shot, showing results and live opponent shots, and recovering after a refresh or a dropped stream.

### Modified Capabilities
- `integration-events`: "The system publishes a fixed catalog of integration events" gains `game.turn-changed.v1`, next to `game.player-ready.v1` that #17 adds. "Each event carries a minimal payload" gains `hit` on shot fired, the turn changed payload, and the player ready payload from #17. Both deltas reproduce the requirement as #17 leaves it.

## Impact

- **Games module** (`src/Games/`):
  - `Games.Abstractions`: `FireShotRequest`, `FireShotResponse`.
  - `Games`: `Game.FireShot`, the `FireShot` feature slice, the `POST /games/{code}/shots` endpoint in `GamesEndpoints`.
  - `Games.Tests`: domain, handler and endpoint tests.
  - No storage change: #17's `GameDocument` v2 already stores `Shot(Column, Row, Hit)`.
- **Integration events** (`src/Core/HexMaster.Battleship.IntegrationEvents`): `GameShotFiredEvent` gains `bool Hit`; new `GameTurnChangedEvent`; catalog, catalog tests and README updated.
- **Frontend** (`src/Frontend/src/app/`):
  - `events/realtime-event-types.ts` gains `game.turn-changed.v1`.
  - `games/`: `GamesApi.fireShot`, the `GamePlayPhase` component tree, a shot-confirm snackbar, store intents on #17's `GamePage` store.
  - Built by the `angular-frontend` agent.
- **Gateway**: the existing `games` prefix covers the new path. No route change.
- **Depends on** (must be applied first): `issue-17-placement-phase` and through it `issue-16-join-game` (optimistic concurrency) and `issue-13-create-new-game`. The integration-events delta is written against #17's version of the catalog.
- **Followed by**: #19 (game end), which hooks a completion check into `FireShot` and replaces the turn change with `game.ended.v1` on the winning shot.
- **Out of scope**: sinking ships ("sunk" state, sunk announcements), winning and ending a game (#19), shot timers or inactivity, spectators, sound.
- **Assumed**:
  - The success status is `200 OK`, not `201`: a shot has no resource URL of its own.
  - The issue's "light grey dot" and "red dot" are the design system's `miss` and `hit` cell markers (`--bs-color-miss`, `--bs-color-hit`); the hit marker keeps its non-colour cue.
  - Adding the non-optional `hit` to `game.shot-fired.v1` stays `v1`: nothing publishes or consumes the event before this change (product-owner decision in the shared brief).
