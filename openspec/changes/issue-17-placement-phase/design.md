# Design

## Context

See proposal.md (Why). The Games module on `development` still holds only template code. This change builds on three changes that are proposed but not yet applied:

- **`issue-13-create-new-game`**: `Game : DomainModel<Guid>, IGame` (`Create`, `GiveFirstTurnTo`, `SwitchTurn`, `Reconcile`, private `Touch(now, playerId?)`), `Player : IPlayer` with flat `ShipCoordinates` / `ShotCoordinates` of `Coordinate(Column, Row)`, `GameCode`, `IGameRepository`, `Games.Data` with `GameTableRepository` storing a JSON `Document` and `DocumentVersion` 1 in table `games`, `GamesEndpoints` over the `/games` group reading `sub` and `name`, the frontend `GamesApi`, the auth interceptor and the `games/:gameCode` → `GamePlaceholder` route.
- **`issue-16-join-game`**: `Game.Join` and `Game.IsPlayer`, `IGameUpdater.UpdateAsync<T>(code, game => GameMutation<T>, ct)` with ETag-based optimistic concurrency and three attempts, `GameUpdateConflictException` → `409 game-update-conflict`, and `GameProblemTypes` (`https://battleship.hexmaster.nl/problems/<slug>`), which this change extends.
- **`issue-14-integration-events-catalog`** (on `development`): `IntegrationEventCatalog`, `GameStartedEvent(GameId, GameCode, PlayerWithTurnId)`, `EventAudience.Game(...)`, and the frontend `REALTIME_EVENT_TYPES` and `EventsService` (`connect(playerId)`, `events$`, `state`). Nothing in the frontend calls `connect` yet.

Frontend constraints: Angular 22, zoneless, signals, Angular Material 22.2.2 with `@angular/cdk` already a dependency. The design system (`docs/design-system.md` §8) defines `.bs-board` (13x13 CSS grid with A–L / 1–12 headers), `.bs-ship` / `.bs-ship--v` and the placing states `.is-valid`, `.is-invalid`, `.is-placed`. The `angular-frontend` agent builds all frontend work and keeps component data in a local immutable store exposed as read-only signals.

#18 and #19 are written in parallel and already name `GameView`, `Shot(Column, Row, Hit)`, `ShipPlacement` with derived cells, `GameDocument` version 2, `GamePage` and its store. This design fixes those names.

## Goals / Non-Goals

**Goals:**
- One place for the board and fleet rules, used by the domain, the API validation and (mirrored) by the frontend for instant feedback.
- A caller's view built in one function, so the "never the opponent's ships" rule cannot be bypassed by a later endpoint.
- Every write goes through #16's `IGameUpdater`, so simultaneous readies start the game exactly once.
- A `GamePage` store that #18 and #19 extend instead of replace.

**Non-Goals:**
- Server-side push of the full game state. Events only tell the page to reload.
- Optimistic UI rollback logic beyond "reload the game after a refusal".
- Removing a placed ship from the board, or un-ready.

## Decisions

### D1. Project layout additions
```
src/Games/HexMaster.Battleship.Games.Abstractions/
  DomainModels/ShipKind.cs, Orientation.cs, ShipPlacement.cs, Shot.cs, Fleet.cs, Board.cs
  DomainModels/IPlayer.cs                     (Ships, Shots, IsReady replace the coordinate lists)
  DataTransferObjects/GameView.cs, PlayerView.cs, OpponentView.cs, ShipView.cs, ShotView.cs
  DataTransferObjects/PlaceShipRequest.cs
src/Games/HexMaster.Battleship.Games/
  DomainModels/Game.cs                        (+ PlaceShip, MarkReady, private Start)
  Exceptions/PlacementRefusedException.cs     (DomainException with a Reason)
  Services/IFirstTurnPicker.cs, RandomFirstTurnPicker.cs
  Features/GetGame/GetGameQuery.cs, GetGameQueryHandler.cs, GameViewMapper.cs
  Features/PlaceShip/PlaceShipCommand.cs, PlaceShipCommandHandler.cs
  Features/MarkReady/MarkReadyCommand.cs, MarkReadyCommandHandler.cs
  Endpoints/GamesEndpoints.cs                 (+ GET /{code}, PUT /{code}/ships/{kind}, POST /{code}/ready)
  Endpoints/GameProblemTypes.cs               (+ placement types)
src/Games/HexMaster.Battleship.Games.Data/
  Entities/GameDocument.cs                    (version 2) + GameDocumentV1.cs and the upgrade
src/Core/HexMaster.Battleship.IntegrationEvents/
  Games/GamePlayerReadyEvent.cs, catalog entry, README row
```

### D2. Ship and board model
- `ShipKind` enum: `Carrier`, `Battleship`, `Cruiser`, `Submarine`, `Destroyer`. `Fleet.LengthOf(kind)` and `Fleet.Kinds` are the single source of lengths (5, 4, 3, 3, 2).
- `Orientation` enum: `Horizontal`, `Vertical`.
- `ShipPlacement(ShipKind Kind, Coordinate Origin, Orientation Orientation)` is a record with a computed `Cells` (`IReadOnlyList<Coordinate>`). Cells are never stored; they are derived from the placement, so the stored shape cannot become inconsistent. #19 uses `Cells` for its sunk check.
- `Board.Size = 12` and `Board.Contains(Coordinate)`.
- `Shot(int Column, int Row, bool Hit)` replaces the flat shot coordinates. #18 records it; this change only stores and returns it.
- `IPlayer` exposes `IsReady`, `Ships` (`IReadOnlyList<ShipPlacement>`) and `Shots` (`IReadOnlyList<Shot>`).

- *Alternative: store the cell list per ship.* Rejected. The cells are a function of the placement; storing them invites drift and makes the overlap and rotation checks compare two sources.
- *Alternative: the fleet as configuration.* Rejected. The product owner fixed the classic fleet; a constant is simpler and the specs pin it.

### D3. Domain operations
- **`Game.PlaceShip(Guid playerId, ShipPlacement placement, DateTimeOffset now)`** checks in this order and throws `PlacementRefusedException` with a `Reason`:
  1. state is not `New` → `GameAlreadyStarted`;
  2. the player is ready → `PlayerReady`;
  3. a cell is outside the board → `OutOfBoard`;
  4. a cell overlaps a cell of another ship of the same player (the ship being moved is excluded) → `Overlap`.

  Otherwise it replaces the player's placement of that kind (or adds it) and calls `Touch(now, playerId)`.
- **`Game.MarkReady(Guid playerId, IFirstTurnPicker picker, DateTimeOffset now)`** returns a `ReadyOutcome` (`AlreadyReady`, `Ready`, `ReadyAndStarted`):
  1. already ready → `AlreadyReady`, no mutation, no `Touch` (checked first so a repeated ready is harmless in any state);
  2. state is not `New` → `GameAlreadyStarted`;
  3. fewer than five placements → `FleetIncomplete`;
  4. sets `IsReady`, calls `Touch`; when player two exists and both are ready, calls the private `Start(picker, now)`, which sets `Started` and calls #13's `GiveFirstTurnTo(picker.Pick(playerOne, playerTwo), now)`.
- **`IFirstTurnPicker`** is injected so tests are deterministic; `RandomFirstTurnPicker` uses `Random.Shared`. Cryptographic randomness is not needed for who shoots first.
- The caller identity check (`IsPlayer`, from #16) happens in the handlers before the domain call and maps to `NotFound`.

- *Alternative: a separate `StartGame` command triggered by the second ready event.* Rejected. Starting inside the same update as the second ready is what makes the race safe: #16's updater re-runs `MarkReady` on the fresh state, so of two simultaneous readies exactly one stored update sees "both ready" and starts the game (see D5).

### D4. Endpoints and responses
All three endpoints are in `GamesEndpoints`, `RequireAuthorization()`, identity from `sub`. A code that fails `GameCode` validation answers `404` at once, as in #16. The route `kind` is parsed case-insensitively into `ShipKind`; `orientation` in the body is a case-insensitive string enum.

| Endpoint | Success | Refusals |
|---|---|---|
| `GET /games/{code}` | `200` `GameView` | `404 game-not-found` (unknown, malformed, not a player) |
| `PUT /games/{code}/ships/{kind}` | `200` `GameView` | `400` validation (kind, orientation, missing/non-integer column or row), `400 placement-out-of-board`, `400 placement-overlap`, `404 game-not-found`, `409 player-ready`, `409 game-already-started`, `409 game-update-conflict` |
| `POST /games/{code}/ready` | `200` `GameView` | `404 game-not-found`, `409 fleet-incomplete`, `409 game-already-started`, `409 game-update-conflict` |

Returning the view from the writes lets the frontend replace its state with the server's after every save without a second request.

`GameView`:
```json
{ "gameId": "…", "gameCode": "K7Q2M9XA", "state": "New",
  "me": { "playerId": "…", "playerName": "Ann", "ready": false, "hasTurn": false,
          "ships": [{ "kind": "carrier", "column": 0, "row": 0, "orientation": "horizontal" }],
          "shots": [] },
  "opponent": { "playerId": "…", "playerName": "Bob", "ready": true, "hasTurn": false, "shots": [] } }
```
`GameViewMapper.ToView(IGame game, Guid callerId)` is the only producer of `GameView`. `OpponentView` has no ships property at all, so the opponent's fleet cannot leak through serialization. Enums serialize as camelCase strings through a `JsonStringEnumConverter` with `JsonNamingPolicy.CamelCase` on the Games API.

- *Alternative: `204 No Content` from the writes.* Rejected. The page would need a follow-up `GET` after every drop, doubling the requests during placement.
- *Alternative: `PUT /games/{code}/fleet` with all ships at once.* Rejected. The issue asks to save every position change, and a per-ship resource makes each drag one small idempotent request.

### D5. Handlers, concurrency and events
- **`GetGameQueryHandler`** reads with #13's `GetByCodeAsync`, returns `NotFound` when missing or when `!game.IsPlayer(caller)`, otherwise the mapped view. Activity `games.get`.
- **`PlaceShipCommandHandler`** runs `IGameUpdater.UpdateAsync(code, game => { if (!game.IsPlayer(caller)) return NoChange(NotFound); game.PlaceShip(...); return Save(view) })`. Placing the same ship at the same spot still saves; the cost is one write, and treating it as a no-op adds a comparison for no observable gain. Activity `games.place-ship`, tagged `ship.kind`.
- **`MarkReadyCommandHandler`** runs the updater with `MarkReady`; `AlreadyReady` returns `NoChange`. After a saved update it publishes, inside one `try`/`catch` that logs at `Error` with the game code and swallows:
  1. `GamePlayerReadyEvent(gameId, code, playerId)` to `EventAudience.Game(<present player ids>)`;
  2. when the outcome is `ReadyAndStarted`, `GameStartedEvent(gameId, code, playerWithTurnId)` to both players.

  Activity `games.ready`, tagged `game.ready.outcome`; counters `games.player-ready` and `games.started`.
- **Race.** A and B ready at once: each loads a state where the other is not yet ready, so each first attempt yields `Ready` (not started). The first write wins; the second gets a `412`, reloads, re-runs `MarkReady` and now sees both ready → `ReadyAndStarted`. Exactly one stored update sets `Started`, so exactly one `game.started.v1` is published. A handler test with #16's token-honouring in-memory fake asserts this.
- Events are published only after the stored update and only from the attempt that saved, so a retried attempt never publishes twice.

### D6. Document version 2
`GameDocument` v2 per player: `PlayerId`, `PlayerName`, `HasTurn`, `IsReady`, `Ships: [{ Kind, Column, Row, Orientation }]`, `Shots: [{ Column, Row, Hit }]`, `LastActivityOn`. The repository writes `DocumentVersion = 2`. On read it switches on `DocumentVersion`: `1` deserializes `GameDocumentV1` and upgrades it (ready `false`, ship coordinates dropped, shot coordinates → `Hit = false`), `2` deserializes directly, anything else throws. The upgrade is a pure function with its own unit tests.

- *Alternative: a migration job that rewrites stored games.* Rejected. Only development data exists, and read-time upgrade needs no orchestration.
- *Alternative: map v1 ship coordinates onto placements.* Rejected. No v1 code path ever placed ships, so the lists are always empty; guessing ships from loose cells would be dead code.

### D7. Integration event
`GamePlayerReadyEvent(Guid GameId, string GameCode, Guid PlayerId) : IIntegrationEvent` with `EventType => "game.player-ready.v1"`, added to `IntegrationEventCatalog`, the catalog tests (count and names) and the README table. The frontend adds `'game.player-ready.v1'` to `REALTIME_EVENT_TYPES`, otherwise `EventSource` drops it silently.

### D8. Frontend game page (`games/game-page/`)
- **Route.** `games/:gameCode` lazy-loads `GamePage` instead of `GamePlaceholder`, which is deleted with its spec. A `playerGuard` (`CanActivateFn`) reads #7's `PlayerSession`; without a player it returns a `UrlTree` to `/`.
- **`GamesApi`** gains `getGame(code)`, `placeShip(code, kind, placement)` and `markReady(code)`, all returning `GameView`, plus a `GameApiError` mapping problem slugs (`game-not-found`, `placement-out-of-board`, `placement-overlap`, `player-ready`, `game-already-started`, `fleet-incomplete`, `game-update-conflict`) and network failures to a union.
- **`GamePageStore`** (component-scoped) holds one immutable state `{ code, status: 'loading' | 'loaded' | 'not-found' | 'failed', game: GameView | null, pendingSaves: number, message: string | null }` exposed as read-only signals and computed selectors (`phase`, `me`, `opponent`, `placedKinds`, `canReady`). Intents: `load()`, `reload()`, `placeShip(...)`, `rotate(kind)`, `markReady()`. #18 adds shot intents, #19 the ended state.
- **Live updates.** On init the page calls `EventsService.connect(playerId)` (idempotent) and subscribes to `events$` with `takeUntilDestroyed`, filtering on `payload.gameCode === code` and the three types; each match calls `reload()`. An `effect` on `EventsService.state` calls `reload()` when it goes from `reconnecting` to `open`. Reloads are coalesced: a reload requested while one is in flight runs once more after it, never in parallel.
- **Phase switch.** `@switch` on the state: `New` → `PlacementPhase`, `Started` → a "game started" panel (replaced by #18), `Completed`/`Abandoned` → a "game over" panel with a link home (refined by #19).

### D9. Frontend placement screen (`games/placement/`)
- **Components.** `PlacementPhase` (layout), `PlacementBoard` (the `.bs-board` grid with placed ships), `ShipList` (unplaced ships), `PlayersPanel`. They are presentational: inputs from the `GamePageStore` signals, outputs to its intents. A small `placement-rules.ts` mirrors D2 (lengths, cells, in-board, overlap) for instant feedback; the server stays authoritative.
- **Drag and drop.** `@angular/cdk/drag-drop`: the ship list and each placed ship are `cdkDrag`; the board is one `cdkDropList`. On drop the target cell is computed from the pointer position relative to the board's cell size, minus the grab offset within the ship, so the cell under the grabbed part of the ship stays under the pointer. While dragging, `cdkDragMoved` updates a preview signal that renders the covered cells with `.is-valid` or `.is-invalid`.
  - *Alternative: native HTML5 drag and drop.* Rejected. It does not work with touch on iOS Safari, and mobile is the first target.
  - *Alternative: one `cdkDropList` per cell.* Rejected. 144 drop lists cost more and still need the grab offset.
- **Tap and keyboard.** Clicking or pressing Enter/Space on a ship sets it active (`activeKind` in the store). Clicking or pressing Enter/Space on a cell while a ship is active places it with its origin there. Cells are buttons in a roving-tabindex grid (`role="grid"`, arrow keys move focus, `aria-label` like "C4, empty" or "C4, cruiser"). Escape clears the active ship.
- **Rotation.** The active placed ship shows a Material icon button (`rotate_right`, `aria-label="Rotate cruiser"`). Rotation keeps the origin and toggles the orientation; an invalid result is refused locally with a `MatSnackBar` message ("The carrier would leave the board" / "…would overlap the battleship") and no request.
- **Saving.** Each accepted change applies the placement to the store at once (`pendingSaves + 1`), calls `placeShip`, and on success replaces `game` with the returned view. On any error it shows a snackbar from the error union and calls `reload()`. Saves are sent in order through a per-page queue, so two quick moves of the same ship cannot arrive reversed.
- **Ready.** `canReady` = five placed kinds and `pendingSaves === 0` and not ready. While the ready request is in flight the button is disabled. After success the board, list and rotate control are inert (`cdkDragDisabled`, cells not activatable) and a "Waiting for your opponent" message replaces the button.
- **Players panel.** Two rows built from `me` and `opponent`. The view is caller-centric, so the panel lists the caller first (marked "You") and the opponent second, with "Player 2" greyed out when `opponent` is null. Ready states use an icon plus text, never colour alone.
- **Layout.** Mobile first: one column (players, board, ships). From `--bs-bp-md` a CSS grid with two columns: board spanning both rows on the left, players top right, ships bottom right. All values from the central SASS variables.
- **Ship names.** The UI shows Carrier, Battleship, Cruiser, Submarine, Destroyer, following the fleet in the proposal rather than the design-system showcase names (same lengths).

### D10. Testing
- **Backend unit tests:** `ShipPlacement.Cells`; `Game.PlaceShip` (each reason, move, touching allowed, self-overlap excluded, activity); `Game.MarkReady` (each outcome and reason, start with a fake picker, alone in the game); `GameViewMapper` (opponent without ships, null opponent); the v1 → v2 upgrade; handlers with mocked updater/sender (events and their order, no event on `AlreadyReady`, publish failure swallowed).
- **Race:** two `MarkReady` handlers on the in-memory fake → both ready, `Started` once, one `game.started.v1`.
- **Endpoints (`WebApplicationFactory`, test JWT):** `401`, `404` (unknown, malformed, not a player), each `400` and `409` problem type, `200` bodies, opponent ships absent from the JSON.
- **Repository against Azurite:** v2 round trip and a stored v1 record read back upgraded; skipped when Azurite is unreachable, as in #13.
- **Frontend:** `GamesApi` error mapping, `GamePageStore` (load, not found, reload coalescing, event filtering, save queue, refusal reload), the guard, placement rules, tap and keyboard placement, rotation refusal, ready enablement and read-only state, and a layout check at 360px and 1280px.
- **Coverage:** at least 80% on `HexMaster.Battleship.Games`.

## Risks / Trade-offs

- [#13 and #16 are not applied yet; their final names may differ] → Task 1.1 gates on both being merged to `development` and maps this design's names onto theirs before coding.
- [Optimistic local placement can briefly disagree with the server] → Every error triggers a reload, and the successful response replaces the local state, so the board converges on the server's view.
- [Many saves during a drag session] → Only drops and rotations save, never drag moves. A save is one small point write.
- [Ready events can be missed when the stream is down] → The page reloads on every reconnect, and a refresh always shows the server state.
- [Drag maths depends on rendered cell size] → Cell size is read from the board's bounding box at drop time, not from a constant, so it follows the responsive layout.
- [The players panel cannot say who is player one] → The view is caller-centric by design; "You" and the opponent is what the player needs. If player order matters later, the view gains an additive field.

## Migration Plan

Development only. Stored games of version 1 are upgraded on read and written back as version 2 on their next update. Rollback is reverting the change; version 2 documents would then fail to load in the old code, so after a rollback the `games` table in Azurite is cleared.
