# Tasks

## 1. Preconditions

- [x] 1.1 Confirm `issue-13-create-new-game`, `issue-16-join-game`, `issue-17-placement-phase` and `issue-18-game-play-page` are archived (`openspec/changes/archive/`) and merged into `development`; stop and report if any is missing
- [x] 1.2 Read the archived #17/#18 specs and the Games and frontend code they produced, and write down the actual names this design refers to (`Game.FireShot` return type, fire-shot handler, shot response DTO, game view DTO, `GameDocument`, `GamePage` store, `GamePlayPhase`); verify by listing the mapping in the first implementation commit message body

## 2. Domain: end detection and winner

- [x] 2.1 Add `WinnerId` (`Guid?`) to `IGame` and `Game`, restored by `Reconcile`; verify a `Reconcile` round-trip unit test with and without a winner passes
- [x] 2.2 Extend `Game.FireShot` with the fleet-sunk check and a private `Complete(winnerId, now)` that sets `Completed`, the winner, clears both turns and touches the shooter; return a `ShotOutcome` (or #18's type) with `GameCompleted`; verify unit tests: last cell completes the game (state, winner, no turn, activity), a non-final hit and a miss switch the turn, `New`/`Started` games have no winner
- [x] 2.3 Pin the refusal of a shot on a `Completed` game (for winner and loser) with a unit test asserting the `DomainException` and an unchanged game

## 3. Persistence

- [ ] 3.1 Add optional `WinnerId` to `GameDocument` (keep `DocumentVersion` 2) and map it in the repository; verify Azurite storage tests (skipped with a reason when unreachable): a completed game round-trips with state, winner and no turn, and a version-2 document without `WinnerId` reads with `null`

## 4. Fire-shot handler, events and API

- [ ] 4.1 In the fire-shot handler, after a successful save publish `GameShotFiredEvent`, then `GameEndedEvent(Completed, winnerId)` to `EventAudience.Game(p1, p2)` instead of `GameTurnChangedEvent` when the game completed; each publish in its own try/catch logging at `Error` with the game code; verify handler tests: order shot-fired → ended, no turn-changed, correct audience and payload, an ended-publish failure is logged and swallowed, a retry that finds the game completed returns the conflict result
- [ ] 4.2 Add the `game.completed` activity tag and the `games.completed` counter on the fire-shot path; verify a handler test (or metric listener) observes one increment per completed game
- [ ] 4.3 Add `state` to the shot response DTO and `winnerId` to the game view DTO, mapped in the slices; verify `WebApplicationFactory` endpoint tests: winning shot response has `state` `Completed`, ordinary shot `Started`, `GET /games/{code}` returns `winnerId` (null while running, winner when completed, no opponent ships), and a shot on a completed game answers `409` with problem details and publishes nothing
- [ ] 4.4 Run `dotnet build src/Battleship.slnx` and `dotnet test` for the Games tests with coverage; verify all green and `HexMaster.Battleship.Games` coverage stays at least 80%

## 5. Frontend: ended state (via the `angular-frontend` agent)

- [ ] 5.1 Add `winnerId` to the `GamesApi` game view type and `state` to the shot response type; verify `ng build` succeeds
- [ ] 5.2 Extend the `GamePage` store with `winnerId`, an idempotent `endGame(winnerId)` intent and computed `isEnded` / `outcome`; call it from the `game.ended.v1` subscription (matching game code only, followed by one reconciling `getGame` reload), from a shot response with state `Completed`, and from loading a `Completed` game; verify Vitest specs for idempotence, won/lost/neutral outcomes, ignoring other game codes, response-driven end, load of a completed game and a failed reload keeping the boards
- [ ] 5.3 Give `GamePlayPhase` a `readonly` input (no clickable cells, no snackbar, no turn indicator, no automatic tab switch) and render it from `GamePage` for `Completed` games; verify Vitest specs that activating a cell sends no shot and the boards show the winning hit

## 6. Frontend: result dialog (via the `angular-frontend` agent)

- [ ] 6.1 Create `games/game-result-dialog/GameResultDialog` (Material dialog, "You won" / "You lost" / "The game has ended", one primary "Close" with `cdkFocusInitial`, backdrop click disabled, central SASS variables); verify Vitest specs for the three texts and initial focus
- [ ] 6.2 Open the dialog from an `effect()` on `outcome()` guarded to open once per page, and navigate to `/` on `afterClosed()`; verify Vitest specs: one dialog when response and event both arrive, Close and Escape both navigate to `/`
- [ ] 6.3 Run `ng test` and `ng build` for `src/Frontend`; verify both pass

## 7. Integration check

- [ ] 7.1 Run the Aspire AppHost, play a game in two browsers until one fleet is sunk; verify the winner sees "You won", the loser "You lost", both boards are read only behind the dialog, a refresh shows the dialog again, and Close returns both to the landing page
