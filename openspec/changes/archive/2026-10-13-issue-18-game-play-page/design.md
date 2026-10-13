# Design

## Context

See proposal.md (Why). State this change builds on (applied and archived before it, in order 13 → 16 → 17 → 18):

- **`issue-13-create-new-game`**: the `Game` aggregate (`GameState`, `PlayerOne`/`PlayerTwo` with `HasTurn`, `SwitchTurn(now)` that requires `Started` and exactly one holder, `Touch(now, playerId?)`, `Reconcile`), `GameCode`, `GamesEndpoints` with identity from `sub`, the `DomainException` base, the module's `ActivitySource` and meter.
- **`issue-16-join-game`**: `IGameUpdater.UpdateAsync(code, game => GameMutation<T>, ct)` with ETag compare-and-swap and up to three re-runs of the mutation on fresh state; `GameUpdateConflictException` → `409 game-update-conflict`; `GameProblemTypes` (`https://battleship.hexmaster.nl/problems/<slug>`); `Game.IsPlayer`.
- **`issue-17-placement-phase`**: ship placements with derived cells, `Shot(Column, Row, Hit)` on each player, `GameDocument` version 2, `Game.Start` giving a random first turn, `GET /games/{code}` with the caller's view (`me.ships`, `me.shots`, `opponent.shots`, `hasTurn` flags), the frontend `GamePage` container with its component-scoped store, its subscription to `EventsService.events$`, and a "game started" placeholder for `Started`.
- **On `development` today**: `GameShotFiredEvent(GameId, GameCode, PlayerId, Column, Row)` in the catalog (#14), `EventAudience.Game(...)`, `REALTIME_EVENT_TYPES` in `src/Frontend/src/app/events/`, `EventsService.state` (`'disconnected' | 'connecting' | 'open' | 'reconnecting'`), and the design tokens `--bs-color-miss`, `--bs-color-hit`, `--bs-duration-*`, `--bs-ease-standard`.

#17 is written in parallel with this change. Where this design names its types (`GameView`, `GamePageStore`, `ShipPlacement.Cells`, `Coordinate`), the implementer uses the names #17 finally chose. #19 hooks into `Game.FireShot` and the fire-shot handler designed here.

## Goals / Non-Goals

**Goals:**
- One aggregate method owns every shot rule (state, turn, board, repeat cell, hit), so #19 can extend it in one place.
- Exactly one shot per turn under double submits and racing requests, using #16's updater unchanged.
- A play view that is correct from the server state alone (load, refresh, reconnect) and uses events only to be live.

**Non-Goals:**
- Sunk-ship detection or announcements, ending the game (#19).
- Optimistic UI: a marker is only drawn after the server answered or an event arrived.
- Replaying missed events on the stream; the page reloads instead.

## Decisions

### D1. Project layout additions (feature slices, ADR 0009)
```
src/Games/HexMaster.Battleship.Games.Abstractions/
  DataTransferObjects/FireShotRequest.cs     (int? Column, int? Row)
  DataTransferObjects/FireShotResponse.cs    (int Column, int Row, bool Hit)
src/Games/HexMaster.Battleship.Games/
  DomainModels/Game.cs                       (+ FireShot)
  DomainModels/ShotOutcome.cs                (Coordinate At, bool Hit)
  Exceptions/ShotRefusedException.cs         (DomainException with a Reason)
  Features/FireShot/FireShotCommand.cs, FireShotCommandHandler.cs, FireShotResult.cs
  Endpoints/GamesEndpoints.cs                (+ POST /{code}/shots)
  GameProblemTypes.cs                        (+ game-not-started, not-your-turn, cell-already-shot)
src/Shared/HexMaster.Battleship.IntegrationEvents/
  Games/GameShotFiredEvent.cs                (+ bool Hit)
  Games/GameTurnChangedEvent.cs              (new)
```

### D2. Domain: `Game.FireShot`
`ShotOutcome Game.FireShot(Guid playerId, Coordinate at, DateTimeOffset now)` checks in this order, throwing `ShotRefusedException(Reason)` and mutating nothing on a refusal:
1. **State is not `Started`** → `Reason.GameNotStarted`.
2. **Caller does not have the turn** → `Reason.NotYourTurn`. The endpoint already filtered non-players, so the caller is one of the two.
3. **Caller already has a shot at `at`** → `Reason.CellAlreadyShot`.
4. Otherwise `hit = opponent.Ships.SelectMany(s => s.Cells).Contains(at)`, append `Shot(at.Column, at.Row, hit)` to the shooter, call `SwitchTurn(now)` (which also touches the game), then `Touch(now, playerId)` for the shooter's last activity. Returns `ShotOutcome(at, hit)`.

The board bounds are not checked here: `Coordinate` (from #17) cannot be built outside 0..11, so the endpoint's validation is the only place an off-board cell is seen.

- *Alternative: check the turn before the state.* Rejected. In a `New` game nobody has the turn, so "not your turn" would mislead; the state is the more fundamental refusal.
- *Alternative: return the updated `Game` and let the handler find the last shot.* Rejected. #19 extends `ShotOutcome` with `GameCompleted`, and a small result keeps the handler free of game rules.

### D3. Fire-shot slice and endpoint
- **Command.** `FireShotCommand(GameCode Code, Guid PlayerId, Coordinate At)`. `FireShotCommandHandler`:
  1. Calls `IGameUpdater.UpdateAsync(code, game => game.IsPlayer(playerId) ? Save(Snapshot(game.FireShot(...), game)) : NoChange(null))`. The snapshot carries the outcome, the game id, both player ids and the id of the player who now has the turn.
  2. `NotFound` or a non-player snapshot → `FireShotResult.NotFound`. A non-player gets `404`, same as #17's `GET`, so the endpoint does not reveal that the game exists.
  3. After the save publishes, each in its own `try`/`catch` that logs at `Error` with the game code and swallows:
     - `GameShotFiredEvent(gameId, code, playerId, column, row, hit)` to `EventAudience.Game(p1, p2)`;
     - then `GameTurnChangedEvent(gameId, code, playerWithTurnId)` to the same audience.
  4. Returns `FireShotResult.Success(new FireShotResponse(column, row, hit))`.
- **Concurrency.** A `ShotRefusedException` thrown inside the mutation propagates out of the updater without a write. On an ETag conflict the updater re-runs the mutation on the reloaded game; for a double submit the reloaded game shows the opponent has the turn, so the second request ends in `NotYourTurn` (spec: "Double submit"). Events are only published after a successful save, so they are never duplicated.
- **Endpoint.** `POST /games/{code}/shots`, `RequireAuthorization()`, body `FireShotRequest`. The code is upper-cased; a code that fails `GameCode` validation is `404`, as in #16. `Column` and `Row` are nullable in the request so a missing value is distinguishable; a missing or out-of-range value returns `TypedResults.ValidationProblem` (`400`) before the handler runs. A non-integer value fails JSON binding, which minimal APIs already answer with `400`.
- **Responses:**

| Outcome | Status | Body |
|---|---|---|
| Recorded | `200` | `{ column, row, hit }` |
| Missing / non-integer / out-of-range cell | `400` | validation problem details |
| Unknown or malformed code, not a player | `404` | problem type `game-not-found` |
| `Reason.GameNotStarted` | `409` | problem type `game-not-started` |
| `Reason.NotYourTurn` | `409` | problem type `not-your-turn` |
| `Reason.CellAlreadyShot` | `409` | problem type `cell-already-shot` |
| `GameUpdateConflictException` | `409` | problem type `game-update-conflict` |

- **Tracing and metrics.** Activity `games.fire-shot` tagged `game.code`, `game.shot.hit` and `game.shot.outcome` (`recorded`, `not-found`, `not-started`, `not-your-turn`, `cell-already-shot`, `conflict`). Counter `games.shots.fired` with a `hit` tag, incremented on a recorded shot.

- *Alternative: `201 Created`.* Rejected (proposal assumption): a shot has no URL of its own.
- *Alternative: let the frontend derive the turn from `game.shot-fired.v1`.* Rejected. A separate turn event keeps "who has the turn" a server decision; #19 replaces it by `game.ended.v1` on the winning shot without the frontend guessing.

### D4. Integration events
- `GameShotFiredEvent` gains `bool Hit` as the last positional parameter. The type stays `game.shot-fired.v1`: nothing publishes or consumes it before this change (proposal assumption).
- New `GameTurnChangedEvent(Guid GameId, string GameCode, Guid PlayerWithTurnId)` with `EventType => "game.turn-changed.v1"`, added to `IntegrationEventCatalog.Events` after `GameShotFiredEvent`, and to the README table. #17 adds `GamePlayerReadyEvent` to the same list; the implementer merges both.
- Catalog and serialization tests pin the new type name and the camelCase payloads from the `integration-events` delta.
- The frontend's `REALTIME_EVENT_TYPES` gains `'game.turn-changed.v1'`; without it `EventSource` drops the event.

### D5. Frontend: store and data flow (implemented by the `angular-frontend` agent)
- **`GamesApi.fireShot(code, column, row): Promise<FireShotResponse>`** posts to `/api/games/${code}/shots`. A `FireShotError` maps the response: `409` slugs `not-your-turn`, `cell-already-shot`, `game-not-started`, `game-update-conflict` to their own values; `404` → `'not-found'`; everything else, including network errors → `'failed'`.
- **`GamePageStore`** (#17) gains, in its single immutable state:
  - `target: { column, row } | null` and `shotStatus: 'idle' | 'firing'`;
  - intents `selectTarget(cell)`, `clearTarget()`, `fireAtTarget()`, `applyShotFired(payload)`, `applyTurnChanged(playerWithTurnId)`;
  - computed `myTurn`, `myShots`, `opponentShots`, `canShoot` (`state === 'Started' && myTurn && shotStatus === 'idle'`).
- **Rules in the store, not the components:**
  - `selectTarget` is a no-op when `!canShoot` or the cell is in `myShots`.
  - `fireAtTarget` sets `firing`, calls the API, and on success appends the shot to `myShots` and clears the target. It does **not** change the turn: the turn moves only by `game.turn-changed.v1` or a reload, so no shot is possible until then (spec: "accept no further shot until a turn change"). On a `409` it reloads the game; on any error it returns to `idle` with an error for the page to show.
  - `applyShotFired` adds the shot to `myShots` or `opponentShots` by `playerId`, skipping a shot already present at the same cell for the same shooter (de-duplicates the own echo and replays).
  - `applyTurnChanged` sets both `hasTurn` flags from the id.
- **Events.** The page's existing `events$` subscription (#17) filters on `payload.gameCode === code` and routes `game.shot-fired.v1` and `game.turn-changed.v1` to the intents above. `game.started.v1` already reloads the game in #17, which moves the page to the play view.
- **Resynchronisation.** The page watches `EventsService.state` with an `effect()` and reloads the game when it moves from `reconnecting` to `open` (only that transition, so the first connect does not reload twice).
- **If no turn event arrives** (publish failure), the player who fired keeps a non-shooting view until the next reload or reconnect. This is the same trade-off #16 and #17 accept.

### D6. Frontend: `GamePlayPhase` component tree
```
games/game-play/
  game-play-phase.ts        (container: turn indicator, tabs, snackbar wiring)
  target-grid.ts            ("You": opponent waters, my shots, selectable cells)
  fleet-grid.ts             ("Opponent": my ships, opponent shots, read only)
  shot-marker.ts            (one hit/miss marker with the enter animation)
```
- `GamePage` renders `GamePlayPhase` for `Started` instead of #17's placeholder. Components take signals from the store and emit intents; they hold no state of their own beyond the selected tab index.
- **Grids** reuse #17's placement grid styling (12x12, column letters A–L, row numbers 1–12) but not its drag-and-drop. Each cell is a `button` in "You" (disabled when not selectable) and a plain cell in "Opponent", with an `aria-label` such as "E7, hit". Hit markers carry a non-colour cue (a filled dot with a cross or ring, as the design system's hit marker defines); misses are a plain light grey dot.
- **Tabs.** `mat-tab-group` with `selectedIndex` bound to a local signal. An `effect()` on `myTurn()` sets it to "You" (0) when true and "Opponent" (1) when false, on first render and on every change; between changes the user may switch freely.
- **Turn indicator.** A text line above the tabs, "Your turn" / "Opponent's turn", in an `aria-live="polite"` region.
- **Confirm snackbar.** An `effect()` on `target()` opens `MatSnackBar.openFromComponent(ShotConfirmSnackbar, { data: { cell } })` with no auto-dismiss, or updates the label when the target moves. "Shoot" calls `fireAtTarget()`, "Cancel" calls `clearTarget()`; the snackbar is dismissed when the target is cleared. Errors use a separate, auto-dismissing snackbar after the confirm one closes.
  - *Alternative: `MatSnackBar.open(message, 'Shoot')`.* Rejected. The simple snackbar has one action; the issue requires Cancel and Shoot.
- **Animation.** A new marker gets an enter animation (scale and fade) using `--bs-duration-base` and `--bs-ease-standard`, implemented with CSS `@starting-style` / a keyframe class on the marker element. Markers present when the view first renders or after a reload do not animate. Under `prefers-reduced-motion: reduce` the animation is `none`.
- **Layout.** Mobile first: turn indicator, tabs, grid stacked; the grid's cell size is derived from the available width with `min()` so 12 columns fit in 360px. All values come from the central SASS variables and tokens.

### D7. Testing
- **Backend** (xUnit, Moq, Bogus, factories under `Factories/`):
  - `Game.FireShot`: hit, miss, turn switched after both, refused when `New`/`Completed`, not your turn, repeated cell (turn kept), both players may hit the same coordinates, activity touched.
  - `FireShotCommandHandler`: events in order with the `Game` audience and payloads; each publish failure logged and swallowed independently; non-player → not found; no events on a refusal.
  - Endpoint (`WebApplicationFactory`, test-signed JWT): `401`, `404` (unknown, malformed, non-player), `400` (missing, out of range, non-integer), each `409` problem type, `200` body.
  - Race: two handlers against #16's in-memory repository fake that honours tokens → one recorded shot, one `not-your-turn`, one pair of events.
  - Integration events: catalog contains `game.turn-changed.v1`; serialization of `hit` and the turn changed payload.
  - Coverage on `HexMaster.Battleship.Games` stays at least 80%.
- **Frontend** (Vitest via `ng test`): `GamesApi.fireShot` error mapping; store intents (selection rules, firing guard, no turn change on response, de-duplication, turn change, `409` reload); event routing and game-code filtering; reload on reconnect; tab following the turn; snackbar Cancel/Shoot/move; marker states and accessible labels; no animation under reduced motion.

## Risks / Trade-offs

- [#17 is written in parallel; type names and the store shape may differ] → The first task gates on #17 being merged and maps this design's names onto #17's before coding.
- [Adding a non-optional field to `game.shot-fired.v1` without a version bump] → Nothing consumes it yet (product-owner decision). The catalog tests pin the new payload.
- [A lost `game.turn-changed.v1` leaves both pages without the turn] → Logged with the game code; any reload or reconnect restores the turn from `GET /games/{code}`. An outbox stays a later change.
- [The shooter's own `game.shot-fired.v1` echo duplicates the response] → De-duplicated by shooter and cell in the store.
- [The confirm snackbar covers the bottom rows on small phones] → The grid area gets bottom padding equal to the snackbar height while a target is set.
- [`effect()` driving tab selection could fight a manual tab change] → It reacts only to `myTurn()` changes, not to the tab index, so manual selection holds until the turn moves.

## Migration Plan

Development only. No storage change: #17's `GameDocument` v2 already stores shots with a hit flag. The event payload change is additive on the wire. Rollback is reverting the change; stored shots stay readable by #17's code.
