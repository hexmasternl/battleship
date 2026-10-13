# Tasks

## 1. Prerequisites

- [x] 1.1 Confirm `issue-13-create-new-game`, `issue-16-join-game` and `issue-17-placement-phase` are merged into `development` (`Game.SwitchTurn`, `IGameUpdater`, `GameProblemTypes`, `Coordinate`, ship placements with cells, `Shot(Column, Row, Hit)`, `GET /games/{code}`, `GamePage` and its store); stop and report if any is missing; write down in the pull request how this design's names (`GameView`, `GamePageStore`, `ShipPlacement.Cells`, `Coordinate`) map onto the merged code; verify `dotnet build src/Battleship.slnx` and `npm run build` in `src/Frontend` pass on the branch before any change

## 2. Integration events

- [x] 2.1 Add `bool Hit` to `GameShotFiredEvent`, add `GameTurnChangedEvent(GameId, GameCode, PlayerWithTurnId)` = `game.turn-changed.v1` to the catalog after the shot event, and update the README table (design D4); verify catalog tests list `game.turn-changed.v1` and serialization tests show `hit` and the turn changed payload in camelCase
- [x] 2.2 Add `'game.turn-changed.v1'` to `REALTIME_EVENT_TYPES` in `src/Frontend/src/app/events/realtime-event-types.ts`; verify the events service spec registers a listener for it

## 3. Domain

- [x] 3.1 Add `ShotRefusedException` with `Reason` (`GameNotStarted`, `NotYourTurn`, `CellAlreadyShot`), `ShotOutcome`, and `Game.FireShot(playerId, at, now)` with the refusal order, hit detection against the opponent's ship cells, recorded shot, `SwitchTurn` and shooter activity (design D2); verify unit tests for hit, miss, turn switched after both, refusal on `New` and `Completed`, not your turn, repeated cell keeping the turn, both players hitting the same coordinates, and no mutation on any refusal pass

## 4. Fire-shot slice and endpoint

- [x] 4.1 Add `FireShotRequest`/`FireShotResponse` to `Games.Abstractions` and `FireShotCommand`, `FireShotResult` and `FireShotCommandHandler` using `IGameUpdater`, publishing `game.shot-fired.v1` then `game.turn-changed.v1` to `EventAudience.Game(p1, p2)` with independent logged-and-swallowed failures, the `games.fire-shot` activity and the `games.shots.fired` counter (design D3); verify Moq-based handler tests for event order, audience and payloads, each publish failure swallowed, non-player as not found, no events on a refusal, and a race test against the token-honouring in-memory repository (one shot, one `not-your-turn`, one event pair) pass
- [x] 4.2 Add the `game-not-started`, `not-your-turn` and `cell-already-shot` slugs to `GameProblemTypes` and map `POST /games/{code}/shots` in `GamesEndpoints` with `RequireAuthorization`, code normalisation, `400` validation for missing or out-of-range cells, and the response table in design D3; verify `WebApplicationFactory` tests for `401`, `404` (unknown, malformed, non-player), `400` (missing, out of range, non-integer), each `409` problem type and the `200` body pass, and coverage on `HexMaster.Battleship.Games` is at least 80%

## 5. Frontend data and store

- [x] 5.1 Add `GamesApi.fireShot(code, column, row)` and `FireShotError` mapping (design D5); verify `HttpTestingController` specs for the request, the success body and every error mapping pass
- [x] 5.2 Extend the `GamePage` store with `target`, `shotStatus`, `selectTarget`, `clearTarget`, `fireAtTarget`, `applyShotFired`, `applyTurnChanged` and the `myTurn`, `myShots`, `opponentShots`, `canShoot` computeds (design D5); verify store specs for selection rules (turn, already shot, in flight), one request per confirm, no turn change on the response, de-duplication by shooter and cell, turn change from the event, error return to idle, and reload after a `409` pass
- [x] 5.3 Route `game.shot-fired.v1` and `game.turn-changed.v1` for this game code from the page's `events$` subscription to the store, ignore other codes, and reload the game when `EventsService.state` goes from `reconnecting` to `open`; verify page specs for routing, game-code filtering, the `game.started.v1` switch to play and a single reload on reconnect pass

## 6. Frontend game play view

- [x] 6.1 Add `games/game-play/` with `GamePlayPhase`, `TargetGrid`, `FleetGrid` and `ShotMarker`, render it from `GamePage` for `Started` in place of #17's placeholder, with the "You"/"Opponent" tabs following `myTurn()`, the `aria-live` turn indicator, accessible cell labels and hit markers with a non-colour cue (design D6); verify component specs for the content of each tab, opponent ships never shown, tab selection on open and on turn changes with manual switching in between, and cell labels pass
- [x] 6.2 Add `ShotConfirmSnackbar` (Cancel / Shoot, cell name, no auto-dismiss) opened and updated from the store's `target`, plus error snackbars per `FireShotError` (design D6); verify specs for Shoot sending exactly one request, Cancel sending none, the target moving, and no snackbar without the turn or on an already shot cell pass
- [x] 6.3 Add the marker enter animation with `--bs-duration-base` and `--bs-ease-standard`, none for markers present on first render or after a reload, and none under `prefers-reduced-motion: reduce`; lay the view out mobile first with all values from the central SASS variables (design D6); verify specs for animation classes and reduced motion pass, and `npm run build` succeeds

## 7. Integration check

- [x] 7.1 Run `dotnet test src/Battleship.slnx` and `npm test` in `src/Frontend`, then start the AppHost, play a game in two browsers (one at a 360px viewport) up to a few shots each, and confirm: confirmed shots draw hit and miss markers, the opponent's shots appear live on the "Opponent" tab, tabs follow the turn, a second shot in the same turn is refused, a refresh restores the same view, and the grid fits without horizontal scrolling; verify all pass and record the observations in the pull request
