# Tasks

## 1. Prerequisites

- [x] 1.1 Confirm `issue-13-create-new-game` and `issue-16-join-game` are merged into `development` (`Game`, `Player`, `GameCode`, `IGameRepository`, `GameTableRepository`, `Game.IsPlayer`, `IGameUpdater`, `GameProblemTypes`, `GamesEndpoints`, `GamesApi`, the `games/:gameCode` placeholder route); stop and report if either is missing; map the names in design.md onto the merged code where they differ; verify `dotnet build src/Battleship.slnx`, `dotnet test src/Battleship.slnx` and the frontend tests pass before any change

## 2. Ship and board model

- [x] 2.1 Add `ShipKind`, `Orientation`, `Fleet` (kinds and lengths 5, 4, 3, 3, 2), `Board` (size 12, `Contains`), `ShipPlacement` with derived `Cells`, and `Shot(Column, Row, Hit)` to `Games.Abstractions` (design D2); verify unit tests for the fleet composition, horizontal and vertical cells, and board bounds pass
- [x] 2.2 Replace `IPlayer`'s flat ship and shot coordinates with `IsReady`, `Ships` and `Shots` and update `Player`, `Game.Create`, `Game.Join` and `Game.Reconcile` accordingly; verify the existing #13/#16 domain tests, adjusted to the new shape, pass and a new game has a player one that is not ready with no ships and no shots

## 3. Placement and ready in the domain

- [x] 3.1 Add `Game.PlaceShip(playerId, placement, now)` and `PlacementRefusedException : DomainException` with `Reason` (`GameAlreadyStarted`, `PlayerReady`, `OutOfBoard`, `Overlap`) checked in the order of design D3; verify unit tests for first placement, move, rotate in place, touching ships allowed, a ship moving onto its own cells, each refusal reason, placing while alone, and activity touched on success pass
- [x] 3.2 Add `IFirstTurnPicker`/`RandomFirstTurnPicker` and `Game.MarkReady(playerId, picker, now)` returning `ReadyOutcome` (`AlreadyReady`, `Ready`, `ReadyAndStarted`) with refusals `GameAlreadyStarted` and `FleetIncomplete`, starting the game through `GiveFirstTurnTo` when both players are ready (design D3); verify unit tests for each outcome, each refusal, ready while alone keeping `New`, start with a fake picker for either player, and `RandomFirstTurnPicker` returning both players over many draws pass

## 4. Storage version 2

- [x] 4.1 Move `GameDocument` to version 2 (ready flag, ship placements, shots with hit), keep `GameDocumentV1` with a pure upgrade function, and switch on `DocumentVersion` when reading in `GameTableRepository` (design D6); verify unit tests for the upgrade (ready false, ships dropped, shots as misses, id/code/state/activity kept) and Azurite-backed tests for a v2 round trip and a stored v1 record read back and rewritten as v2 pass, or skip with a clear reason when Azurite is unreachable

## 5. Integration event

- [x] 5.1 Add `GamePlayerReadyEvent(GameId, GameCode, PlayerId)` = `game.player-ready.v1` to `src/Shared/HexMaster.Battleship.IntegrationEvents`, the catalog and the README table (design D7); verify the catalog and serialization tests, updated for the new type and its camelCase payload, pass

## 6. Get game slice and endpoint

- [x] 6.1 Add the `GameView`, `PlayerView`, `OpponentView` (no ships property), `ShipView` and `ShotView` DTOs, `GameViewMapper.ToView(game, callerId)`, and `GetGameQuery`/`GetGameQueryHandler` with the `games.get` activity (design D4, D5); verify unit tests for the mapper (creator alone with null opponent, player two's view, opponent ships absent) and the handler (not found, not a player, success, no write) pass
- [x] 6.2 Add `GET /games/{code}` to `GamesEndpoints` with camelCase string enums on the Games API JSON options; verify `WebApplicationFactory` tests for `401`, `404` unknown, malformed and not-a-player, lower-case code, and a `200` body whose JSON contains none of the opponent's ships pass

## 7. Place ship slice and endpoint

- [x] 7.1 Add `PlaceShipRequest`, `PlaceShipCommand`/`PlaceShipCommandHandler` using `IGameUpdater` and returning the caller's view, with the `games.place-ship` activity tagged `ship.kind` (design D5); verify Moq-based handler tests for success, not found, not a player, and a refusal propagating without a write pass
- [x] 7.2 Add the placement problem types (`placement-out-of-board`, `placement-overlap`, `player-ready`, `fleet-incomplete`) to `GameProblemTypes` and `PUT /games/{code}/ships/{kind}` to `GamesEndpoints` with case-insensitive kind and orientation and `400` validation for unknown kind, unknown orientation and missing or non-integer column or row (design D4 table); verify endpoint tests for `401`, `404`, `200` with the view, each `400` and `409` problem type, and `Carrier` in the route accepted pass

## 8. Ready slice and endpoint

- [x] 8.1 Add `MarkReadyCommand`/`MarkReadyCommandHandler` using `IGameUpdater` (`AlreadyReady` → no write), publishing `game.player-ready.v1` to the present players and then, when started, `game.started.v1` to both, with publish failures logged (game code) and swallowed, the `games.ready` activity and the `games.player-ready` and `games.started` counters (design D5); verify handler tests for the events, their audiences and order, no event on `AlreadyReady`, ready while alone targeting only the caller, and a swallowed publish failure pass
- [x] 8.2 Add `POST /games/{code}/ready` to `GamesEndpoints`; verify endpoint tests for `401`, `404`, `200` with `me.ready` true, `409 fleet-incomplete`, `409 game-already-started` and ready-again `200` pass
- [x] 8.3 Add a race test running two `MarkReady` handlers for both players against #16's in-memory repository fake that honours concurrency tokens; verify both answer success, the game is `Started` once with exactly one turn holder, exactly one `game.started.v1` is sent, and coverage on `HexMaster.Battleship.Games` is at least 80%

## 9. Frontend game page (angular-frontend agent)

- [x] 9.1 Add `'game.player-ready.v1'` to `REALTIME_EVENT_TYPES`, and the `GameView` types, `GamesApi.getGame`, `placeShip` and `markReady`, and the `GameApiError` mapping of every problem slug and network failure (design D8); verify `HttpTestingController` specs for each call and each error mapping pass
- [x] 9.2 Add `playerGuard` and replace the `GamePlaceholder` route with a lazy `GamePage` (delete the placeholder and its spec); verify route specs for redirect to `/` without a player (no game request sent) and `GamePage` with one pass
- [x] 9.3 Add the component-scoped `GamePageStore` (state, selectors, `load`, coalesced `reload`) and `GamePage` with loading, not-found (link home), failed (retry) and the phase switch (`New` → placement, `Started` → "game started" panel, `Completed`/`Abandoned` → "game over" panel); connect `EventsService` on init and reload on `game.joined.v1`, `game.player-ready.v1` and `game.started.v1` for this code and on reconnect-to-open (design D8); verify store and page specs for each load outcome, each phase, event filtering by code and type, reconnect reload, and coalesced reloads pass

## 10. Frontend placement screen (angular-frontend agent)

- [x] 10.1 Add `placement-rules.ts` mirroring the fleet, cells, board bounds and overlap rules; verify unit specs matching the backend rule cases pass
- [x] 10.2 Add `PlacementPhase`, `PlacementBoard` (`.bs-board`, A–L / 1–12 labels, roving-tabindex grid with coordinate labels) and `ShipList` (unplaced ships with name and length), with the mobile-first layout and the wide-screen grid from `--bs-bp-md`, styled only through the central SASS variables (design D9); verify component specs for an empty board with five listed ships, placed ships shown on the board and removed from the list, and cell labels pass
- [x] 10.3 Add drag and drop with `@angular/cdk/drag-drop` (list and placed ships draggable, one board drop list, grab-offset drop maths, `.is-valid`/`.is-invalid` preview, invalid drop reverts) plus tap and keyboard placement through the active ship (design D9); verify specs for drag from the list, move on the board, invalid drop, tap-then-cell placement, keyboard placement and Escape clearing the active ship pass
- [x] 10.4 Add the active ship border and rotate control (90° clockwise, origin fixed), refusing an invalid rotation locally with a snackbar message, and the ordered save queue that applies changes at once, replaces the state with the returned view, and on any error shows a message and reloads (design D9); verify specs for rotation, refusal at the edge and on overlap, one active ship at a time, one request per change in order, and reload after a `409`
- [ ] 10.5 Add `PlayersPanel` (caller marked "You", opponent or greyed-out "Player 2", ready state by icon and text) and the "Ready" action (enabled only with five ships and no pending save, single in-flight request, read-only board and "Waiting for your opponent" after success, also after a refresh); verify specs for each panel state, the disabled and enabled ready action, double-click sending one request, and the read-only board pass, and `ng build` succeeds

## 11. Integration check

- [ ] 11.1 Run the AppHost and, with two browser profiles through the gateway, create and join a game, place all ships by drag, by tap and by keyboard, rotate, refresh mid-placement, ready one player, then the other; verify both screens show the opponent's join and ready live, a refresh restores the same board, both pages switch to the "game started" panel, and the Games API logs show `game.player-ready.v1` twice and `game.started.v1` once; check the layout at 360px and 1280px wide
