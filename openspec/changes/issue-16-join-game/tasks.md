# Tasks

## 1. Prerequisites

- [x] 1.1 Confirm `issue-13-create-new-game` is merged into `development` (`Game`, `GameCode`, `IGameRepository`, `GameTableRepository`, `GamesEndpoints`, `GamesApi.createGame`, `CreateGameDialog`, the `games/:gameCode` placeholder route); stop and report if it is missing; verify `dotnet build src/Battleship.slnx` and the frontend tests pass on the branch before any change

## 2. Join in the domain

- [x] 2.1 Add `Game.Join(playerId, playerName, now)` returning `JoinOutcome` (`Joined`, `AlreadyPlayer`), `Game.IsPlayer(playerId)`, and `GameJoinRefusedException : DomainException` with a `Reason` (`NotJoinable`, `GameFull`), checked in the order of design D2; verify unit tests for success, `AlreadyPlayer` for player one and player two (also when `Started`), `NotJoinable` for `Started`, `Completed` and `Abandoned`, `GameFull`, and activity touched only on a successful join pass

## 3. Optimistic concurrency

- [x] 3.1 Add `GameForUpdate` and `IGameRepository.GetForUpdateAsync`/`TryUpdateAsync` to `Games.Abstractions` (token as an opaque string), and implement them in `GameTableRepository` with the stored ETag and `UpdateEntityAsync(..., new ETag(token), TableUpdateMode.Replace)`, returning `false` on `412` (design D3); verify Azurite-backed tests for an update with a fresh token and `false` with a stale token pass, or skip with a clear reason when Azurite is unreachable
- [x] 3.2 Add `IGameUpdater`/`GameUpdater` with `GameMutation<T>` (`Save`, `NoChange`), `GameUpdateResult<T>` (incl. `NotFound`), `MaxAttempts = 3`, immediate retries, the `game.update.conflict` activity event, the `games.update.conflicts` counter and `GameUpdateConflictException`; register it in `AddGamesModule` (design D3); verify Moq-based tests for not found, `NoChange` writing nothing, save on the first attempt, conflict then success with `mutate` re-run on the reloaded game, three conflicts throwing, and a `DomainException` propagating without a write pass

## 4. Join slice and endpoint

- [x] 4.1 Add `JoinGameResponse` to `Games.Abstractions`, and `JoinGameCommand`/`JoinGameCommandHandler` in `Features/JoinGame` using `IGameUpdater`, sending `GameJoinedEvent` to `EventAudience.Game(playerOne, playerTwo)` only on a saved join with publish failures logged (game code) and swallowed, plus the `games.join` activity with `game.join.outcome` and the `games.joined` counter (design D4); verify handler tests for the event payload and audience, no event for `AlreadyPlayer`, `NotFound`, and a swallowed publish failure pass
- [x] 4.2 Add `GameProblemTypes` (`game-not-found`, `game-already-started`, `game-full`, `game-update-conflict` under `https://battleship.hexmaster.nl/problems/`) and `POST /games/{code}/join` in `GamesEndpoints` (`RequireAuthorization`, no body, identity from `sub`/`name`, upper-cased code, malformed code answered `404`) mapping outcomes as in the design D4 table; verify `WebApplicationFactory` tests with a test-signed JWT for `401`, `404` unknown and malformed, `200` join and re-join, lower-case code, and the three exact `409` problem types pass
- [x] 4.3 Add a race test running two join handlers against an in-memory repository fake that honours concurrency tokens; verify exactly one join succeeds, the other gets `GameFull`, exactly one event is sent, and coverage on `HexMaster.Battleship.Games` is at least 80%

## 5. Frontend join flow (angular-frontend agent)

- [x] 5.1 Add `GamesApi.joinGame(code)` posting to `/api/games/${code}/join` with no body, and the `JoinGameError` mapping (`404` → `not-found`, `409` slug `game-full` → `full`, slug `game-already-started` → `started`, anything else → `failed`) (design D5); verify `HttpTestingController` specs for success and every error mapping pass
- [x] 5.2 Add `games/join-game-dialog/` (`JoinGameDialog` with its component-scoped `JoinGameStore`: code input upper-cased, stripped to `A-Z0-9`, max 8, initial focus; join enabled only for a valid code; progress; double-submit guard; inline `role="alert"` errors with the design D5 messages; code kept after errors; error cleared on edit; closes with `{ gameCode }`; styles from the central SASS variables); verify store and dialog specs for upper-casing, invalid code disabling join, each error message, retry, ignored second join and cancel pass
- [ ] 5.3 Turn the hero "Join an existing game" into a `bs-btn bs-btn--secondary` button whose handler awaits `ensurePlayer()` (with `PlayerNameDialog` when unknown), opens `JoinGameDialog` and navigates to `/games/{code}` on a result; remove the `games/join` route and delete `ComingSoon` and its spec if nothing else uses it; verify hero specs for the known, unknown-then-join, cancel with focus return and pending-refresh scenarios of the welcome-page delta pass, and `ng build` succeeds

## 6. Integration check

- [ ] 6.1 Run the AppHost, create a game as one player and join it with its code as a second player through the gateway in another browser profile; verify both land on `/games/{code}`, a third player sees "This game already has two players.", and the Games API logs show `game.joined.v1` published once
