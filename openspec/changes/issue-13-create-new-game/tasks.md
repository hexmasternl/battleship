# Tasks

## 1. Prerequisites and projects

- [ ] 1.1 Confirm `issue-7-user-identification`, `issue-11-yarp-local-gateway` and `issue-14-integration-events-catalog` are merged into `development` (Core, `AddBattleshipAuthentication`, the `tables` resource, `PlayerSession`, `provideHttpClient` with the `/api` rewrite, `IIntegrationEventSender`); stop and report if any is missing; verify `dotnet build src/Battleship.slnx` passes on the branch before any change
- [ ] 1.2 Create `src/Games/HexMaster.Battleship.Games.Data` (references `Games.Abstractions`, `Games`, `Aspire.Azure.Data.Tables` at the version #7 uses), add it to `Battleship.slnx`; add Core and integration events references to the Games module, Moq and Bogus to `Games.Tests`; delete the `Class1.cs` stubs and `UnitTest1.cs`; verify the solution builds

## 2. Game domain model

- [ ] 2.1 Add `GameState`, `Coordinate`, `IGame`, `IPlayer` to `Games.Abstractions` and `GameCode` (validated `^[A-Z0-9]{8}$`) plus `IGameCodeGenerator`/`GameCodeGenerator` (`RandomNumberGenerator.GetItems`) to the module (design D2); verify unit tests for code validation and generator format and alphabet pass
- [ ] 2.2 Add `Game : DomainModel<Guid>, IGame` and `Player : IPlayer` with `Create`, `GiveFirstTurnTo`, `SwitchTurn`, `Touch` and `Reconcile` (design D2); verify unit tests for the initial state, the turn rule (switch flips both, refusal when the other holds the turn, refusal while `New`), `LastActivityOn` on every mutation, and a `Reconcile` round trip pass

## 3. Persistence

- [ ] 3.1 Add `IGameRepository` (`TryAddAsync` returning `false` on conflict, `GetByCodeAsync`), `GameEntity`/`GameDocument` (`DocumentVersion` 1) and `GameTableRepository` over table `games` keyed by code with `CreateIfNotExists`, and `AddGamesData` registering the `tables` client (design D4); verify Azurite-backed repository tests (round trip, conflict returns `false`) pass, or skip with a clear reason when Azurite is unreachable
- [ ] 3.2 Wire the Games API in the AppHost to `tables` (`WithReference`/`WaitFor`) and the `players-signing-key` parameter; verify the AppHost starts and the dashboard shows the Games API healthy with the storage reference

## 4. Create game slice and endpoint

- [ ] 4.1 Add `CreateGameCommand`, `CreateGameResponse` and `CreateGameCommandHandler` with the code retry (max 5), `GameCreatedEvent` sent to `EventAudience.Players(creator)` with publish failures logged and swallowed, the `games.create` activity and `games.created` counter (design D3); verify Moq-based handler tests for success, retry on conflict, failure after 5 conflicts, the sent event and audience, and a swallowed publish failure pass
- [ ] 4.2 Add `GamesEndpoints.MapGamesEndpoints` with `POST /games` (`RequireAuthorization`, identity from `sub`/`name`, `201` with `Location: /games/{code}`), `AddGamesModule`, and the new `Program.cs` wiring with `AddBattleshipAuthentication`; remove the weather sample; verify `WebApplicationFactory` tests for `401` without or with an expired token and `201` with body and `Location` using a test-signed JWT pass, and coverage on `HexMaster.Battleship.Games` is at least 80%

## 5. Frontend authenticated requests

- [ ] 5.1 Add `PlayerSession.renew()` sharing the in-flight refresh, and `core/session/auth.interceptor.ts` attaching the bearer token to `/api/` requests except the token endpoints, renewing within 30 s of expiry and retrying once on `401` via an `HttpContextToken`; register it with the gateway interceptor in `app.config.ts` (design D5); verify `HttpTestingController` specs for header present, skip list, renewal before send, one shared renewal for two requests, single retry and no second retry pass

## 6. Frontend create-game flow

- [ ] 6.1 Add `games/games-api.ts` (`createGame` posting `/api/games`) and `games/create-game-dialog/` (`CreateGameDialog` with its component-scoped `CreateGameStore`, confirm/cancel, progress, error with retry, double-confirm guard, styles from the central SASS variables) (design D6); verify dialog and store specs for success result, cancel, failure with retry and ignored second confirm pass
- [ ] 6.2 Turn the hero "Create a new game" into a button whose handler runs `ensurePlayer()` (with `PlayerNameDialog` when unknown), then `CreateGameDialog`, then navigates to `/games/{code}`; keep "Join an existing game" as #7 left it; verify hero specs for known, unknown-then-confirm, cancel with focus return, failure and pending-refresh scenarios of the welcome-page delta pass
- [ ] 6.3 Replace the `games/new` route with `games/:gameCode` lazy-loading a `GamePlaceholder` panel that names the code; update `app.routes.spec.ts`; verify the route specs pass and `npm run build` in `src/Frontend` succeeds

## 7. Integration check

- [ ] 7.1 Run `dotnet test src/Battleship.slnx` and `npm test` in `src/Frontend`, then start the AppHost, create a player and a game from the landing page in a browser, and confirm the redirect to `/games/{code}`, the `games` table row in Azurite with the JSON document, and the `game.created.v1` event on the player's event stream; verify all pass and record the observed game code in the pull request
