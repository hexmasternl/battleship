# Design

## Context

See proposal.md (Why). State on `development` and in the changes this one builds on:

- **Games module.** It holds only template code:
  - `Class1.cs` in `Games` and `Games.Abstractions`;
  - the `/weatherforecast` sample and the dev-only smoke endpoints in `Games.Api` (the smoke endpoints are removed by #14);
  - an empty `UnitTest1.cs`.

  There is no persistence, no authentication and no reference to Core.
- **`issue-7-user-identification`** (proposed) introduces:
  - `src/Core/HexMaster.Battleship.Core` (`ICommandHandler`, `IQueryHandler`, `DomainModel<TId>`, `DomainModelState`, `DomainException`);
  - `AddBattleshipAuthentication()` in ServiceDefaults (HS256 JWT, `sub` = internal user id, `name` = display name, configured from `Players:Tokens`);
  - the AppHost `storage`/`tables` Azurite resource and the `players-signing-key` parameter;
  - the frontend `TokenStorage`, `PlayersApi`, the root `PlayerSession` store (`restore`, `createPlayer`, `ensurePlayer`) and `PlayerNameDialog`;
  - the hero calls to action gated by `ensurePlayer()`.

  #7 leaves the bearer interceptor and access-token renewal to this change. The Players service issues the tokens itself, so `sub` already is the internal `UserId`, and the subject-id lookup of ADR 0010 does not apply.
- **`issue-11-yarp-local-gateway`** (implemented in its worktree) introduces `provideHttpClient`, the `/api/...` → gateway rewrite interceptor and the `games` route prefix, passed through unchanged. Games endpoints therefore live under `/games`.
- **`issue-14-integration-events-catalog`** (proposed) introduces `GameCreatedEvent(GameId, GameCode, PlayerId)` = `game.created.v1`, `IIntegrationEventSender.SendAsync(event, EventAudience, ct)` and `EventAudience.Players(...)`.
- **#17** owns the `/games/{game-code}` page. It will need `ShipCoordinates` with real ship placements.

## Goals / Non-Goals

**Goals:**
- A game aggregate whose invariants hold no matter which later feature mutates it.
- A create slice that is HTTP-free and storage-free in its handler, so it can be unit-tested with mocks.
- One storage record per game that #17 can load by code with a point read.
- A frontend flow that reuses #7's gate and adds no state outside component-local and session stores.

**Non-Goals:**
- Optimistic concurrency between two writers. Only creation writes in this change. The ETag is stored on the record so #17 can use it.
- Listing games, or limiting how many open games a player may have.
- Real-time updates on the landing page.

## Decisions

### D1. Project layout (feature slices, ADR 0009)
```
src/Games/HexMaster.Battleship.Games.Abstractions/
  DataTransferObjects/CreateGameResponse.cs
  DomainModels/IGame.cs, IPlayer.cs, GameState.cs, Coordinate.cs
  Repositories/IGameRepository.cs
src/Games/HexMaster.Battleship.Games/
  DomainModels/Game.cs, Player.cs, GameCode.cs
  Features/CreateGame/CreateGameCommand.cs, CreateGameCommandHandler.cs
  Services/IGameCodeGenerator.cs, GameCodeGenerator.cs
  Endpoints/GamesEndpoints.cs (MapGamesEndpoints over the /games group)
  GamesModuleRegistration.cs (AddGamesModule)
src/Games/HexMaster.Battleship.Games.Data/          (new)
  Entities/GameEntity.cs, GameDocument.cs
  Repositories/GameTableRepository.cs
  GamesDataRegistration.cs (AddGamesData)
```
The Games module references Core, `Games.Abstractions` and the integration events library. `Games.Data` references `Games.Abstractions`, `Games` (for `Game.Reconcile`) and `Aspire.Azure.Data.Tables`. This mirrors #7's Players layout, so both modules read alike.

- *Alternative: put storage in the module project.* Rejected for the same reason as #7: ADR 0013 keeps the module unaware of its storage.

### D2. Domain model
- **`Game : DomainModel<Guid>, IGame`** holds `GameCode`, `State`, `PlayerOne`, `PlayerTwo` (nullable) and `LastActivityOn`.
  - The factory `Game.Create(Guid playerId, string playerName, GameCode code, DateTimeOffset now)` sets state `New`, makes the creator player one with `HasTurn = false`, and sets the activity instant on the game and on player one.
- **`Player : IPlayer`** holds `PlayerId`, `PlayerName`, `HasTurn`, `ShipCoordinates` and `ShotCoordinates` (`IReadOnlyList<Coordinate>`, where `Coordinate(int Column, int Row)`), and `LastActivityOn`. Its setters are private. Only `Game` changes `HasTurn`, through internal methods.
- **Turn rule.** `Game.GiveFirstTurnTo(playerId, now)` succeeds only when nobody has the turn and the game is `Started`. `Game.SwitchTurn(now)` flips both flags in one method, and it requires `Started` and exactly one holder. A violation throws a `DomainException`. Both methods exist now, with tests, so the rule is fixed before #17 and the shooting feature use it. No endpoint calls them in this change.
- **Activity time.** Every public mutation takes `now` (from `TimeProvider` in the handler) and ends with a private `Touch(now, playerId?)`. A test asserts that each mutation updates `LastActivityOn`.
- **`GameCode`** is a small value type that validates `^[A-Z0-9]{8}$`. `GameCodeGenerator` draws 8 characters with `RandomNumberGenerator.GetItems` from the 36-symbol alphabet. A value type is justified here: the code crosses the API, storage and the URL, and an invalid one must not be representable.
- **`ShipCoordinates`** stays a flat list of coordinates. #17 may replace it with ship placements (ship, origin, orientation), and that change to the stored JSON is handled by D4's versioned document.

- *Alternative: keep `HasTurn` only on the game (a `PlayerWithTurn` id).* Rejected. The issue explicitly puts `HasTurn` on each player, and the API model #17 returns follows the domain. The invariant is enforced in one place either way.

### D3. Create slice and endpoint
- **Command.** `CreateGameCommand(Guid PlayerId, string PlayerName)`. `CreateGameCommandHandler : ICommandHandler<CreateGameCommand, CreateGameResponse>` does the following:
  1. Generates a code.
  2. Creates the `Game`.
  3. Calls `IGameRepository.TryAddAsync(game)`, which returns `false` on a key conflict, in which case it regenerates the code. It tries at most 5 times, then throws.
  4. Sends `GameCreatedEvent` with `EventAudience.Players(playerId)` inside a `try`/`catch` that logs the error at `Error` level with the game code and swallows it.
  5. Returns `CreateGameResponse(GameId, GameCode)`.
- **Endpoint.** `GamesEndpoints` maps `POST /games` with `RequireAuthorization()`. It reads `sub` (parsed as `Guid`; anything else gives `401`) and `name` from `ClaimsPrincipal`, dispatches the command, and returns `TypedResults.Created($"/games/{code}", response)`. The endpoint takes no body.
- **API wiring.** `Program.cs` becomes `AddServiceDefaults`, `AddBattleshipAuthentication`, `AddOpenApi`, `AddGamesModule`, `AddGamesData`, and then `MapDefaultEndpoints`, `MapOpenApi` (development) and `MapGamesEndpoints`. The template weather endpoint is removed.
- **Tracing.** Following the OpenTelemetry standard, the handler starts an activity `games.create` from the module's `ActivitySource`, tagged with `game.code`. A counter `games.created` is incremented on success.

Swallowing a publish failure (spec: Game creation is announced) keeps the player's flow working when the broker hiccups. No consumer depends on `game.created.v1` for correctness yet. An outbox is the proper fix, and it is a later change.

### D4. Table storage record
| Table | PartitionKey | RowKey | Columns |
|---|---|---|---|
| `games` | game code | `"game"` | `GameId` (Guid), `State` (string), `LastActivityOn`, `Document` (string, JSON), `DocumentVersion` (int, `1`) |

- **Writing.** `TryAddAsync` uses `AddEntityAsync`. A `409 Conflict` is the uniqueness check and returns `false`, so there is no read-before-write race.
- **The document.** `Document` is `GameDocument`, a plain record mirroring the aggregate, serialized with `JsonSerializerDefaults.Web`. On read it is restored through `Game.Reconcile(...)`.
- **Point reads.** Keying by code makes #17's `GET /games/{code}` a point read. The table is created on first use (`CreateIfNotExists`).
- **Size.** One game's document is far below the 64 KiB string-property limit: two players, at most 144 shots each.
- **Shared resource.** The AppHost adds `.WithReference(tables).WaitFor(tables)` and passes the signing key to the Games API, using the `tables` resource from #7.

- *Alternative: one column per field, players flattened.* Rejected. The user asked for the state as one JSON object, and flattening the coordinate lists into table columns does not fit.
- *Alternative: partition by game id.* Rejected. Every lookup the frontend makes is by code.

### D5. Frontend authentication interceptor
`core/session/auth.interceptor.ts` is a functional `HttpInterceptorFn`, registered in `provideHttpClient(withInterceptors([...]))` next to #11's gateway interceptor. It reads tokens through `TokenStorage`.

Interceptor order: Angular runs interceptors in array order. The auth interceptor is placed **before** the gateway rewrite, so it still sees the `/api/` prefix. It only matches `/api/` requests and skips `/api/players/tokens*` (the token endpoints, wherever #7 finally mounts them under the `players` prefix). The implementer confirms the final token paths from #7 before writing the skip list.

For each matching request:
- If the access token is missing or expires within 30 s and a refresh token exists, it awaits `PlayerSession.renew()`. This is a new intent on #7's store: it shares the in-flight refresh exactly like `restore()`. It is a thin wrapper, because `restore()` already deduplicates concurrent calls.
- It clones the request with `Authorization: Bearer ...`.
- On `401` it calls `renew()` once and repeats the request once, marked with an `HttpContextToken` so the retry is never retried again.

- *Alternative: a separate token-refresh service.* Rejected. #7's `PlayerSession` already owns the refresh, the two-tab rotation handling, and clearing on refusal.

### D6. Frontend create-game flow
- **`games/games-api.ts`.** `GamesApi.createGame(): Promise<CreateGameResponse>` posts to `/api/games`.
- **`games/create-game-dialog/`.**
  - `CreateGameDialog` is an Angular Material dialog with a title ("Create a new game"), a short explanation, Cancel and "Create game" buttons, a progress indicator, and an inline error region with retry.
  - Its component-scoped `CreateGameStore` holds `{ status: 'idle' | 'creating' | 'failed', error }` in one signal. The intent `create()` ignores calls while `creating`.
  - On success the dialog closes with `{ gameCode }`.
  - Styling uses the central SASS variables and the existing `bs-btn` classes.
- **Hero.** The "Create a new game" click handler awaits `ensurePlayer()`, plus #7's `PlayerNameDialog` when needed. It then opens `CreateGameDialog` and calls `router.navigate(['/games', gameCode])` on a result.
  - The call to action becomes a `<button type="button" class="bs-btn bs-btn--primary">`, because it now always opens a dialog and never navigates directly. "Join an existing game" stays a link.
  - *Alternative: keep the link with a prevented default, as #7 does.* Rejected for create. A link whose target is never followed misleads assistive technology and middle-click.
- **Routes.**
  - `games/new` is removed; the wildcard sends old links to the landing page.
  - `games/:gameCode` lazy-loads a `GamePlaceholder` panel that shows the code, styled like `ComingSoon`, until #17 replaces it.
- **Tests.**
  - The dialog store, the dialog and the interceptor are tested with `HttpTestingController` (renewal, shared renewal, single retry, skip list).
  - The hero tests cover known, unknown, cancel, failure and double-confirm.

### D7. Testing (backend)
- **Packages.** Moq and Bogus are added to `Games.Tests`. Factories go under `Factories/`.
- **Unit tests** cover:
  - `Game` (create, the turn rule including refusal paths, activity on every mutation, `Reconcile` round trip);
  - `GameCode` validation and `GameCodeGenerator` (format, and the alphabet over many draws);
  - `CreateGameCommandHandler` (code retry on conflict, failure after 5 conflicts, the event sent with the right audience, a publish failure swallowed and logged);
  - the endpoint (`401` without a token, and `201` with `Location` using a test JWT signed with a test key through `WebApplicationFactory`).
- **Storage tests.** `GameTableRepository` tests run against Azurite. As in #7, they are skipped with a clear reason when Azurite is unreachable.
- **Coverage.** At least 80% on `HexMaster.Battleship.Games`.

## Risks / Trade-offs

- [Three prerequisite changes (#7, #11, #14) must be applied first, and #7 must be archived before this change is archived] → The tasks start with a gate that checks each prerequisite is on `development`. The welcome-page delta is written against #7's version of the requirement.
- [A swallowed publish failure means a missed `game.created.v1`] → It is logged with the game code. Nothing consumes the event for correctness yet. An outbox is a later change.
- [The JSON document shape will change in #17 (ship placements)] → `DocumentVersion` is stored from day one, so a reader can upgrade old documents.
- [Code collisions: 36^8 ≈ 2.8 × 10^12 codes] → The retry loop handles them. Five consecutive collisions are effectively impossible and surface as a `500` rather than a silent overwrite.
- [Interceptor order with #11's gateway rewrite] → A test asserts the header is present on the rewritten request and absent on the refresh request.

## Migration Plan

Development only. No existing game data exists. The `games` table is created on first use in the persistent Azurite container from #7. Rollback is reverting the change and deleting the `games` table from Azurite.
