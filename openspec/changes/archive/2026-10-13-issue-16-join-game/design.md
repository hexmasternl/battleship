# Design

## Context

See proposal.md (Why). State this change builds on, once `issue-13-create-new-game` is applied and archived:

- **Games module** (#13):
  - `Game : DomainModel<Guid>, IGame` with `Create`, `GiveFirstTurnTo`, `SwitchTurn`, `Reconcile` and a private `Touch(now, playerId?)`; `Player : IPlayer`; the `GameCode` value type (`^[A-Z0-9]{8}$`).
  - `IGameRepository` with `TryAddAsync` (`false` on a key conflict) and `GetByCodeAsync`.
  - `Games.Data`: `GameTableRepository` over table `games`, PartitionKey = code, RowKey `"game"`, JSON `Document` + `DocumentVersion` 1, restored through `Game.Reconcile`. #13 stores the ETag with the record but never uses it ("Non-Goals: optimistic concurrency").
  - The `CreateGame` slice, `GamesEndpoints.MapGamesEndpoints` over the `/games` group with identity from `sub` and `name`, the `games.create` activity and the `games.created` counter.
- **Integration events** (#14, on `development`): `GameJoinedEvent(GameId, GameCode, PlayerId, PlayerName)` = `game.joined.v1` is already in the catalog, and `EventAudience.Game(params IEnumerable<Guid>)` gives target kind `game`.
- **Gateway** (#11): the `games` prefix forwards `/api/games/**` unchanged, so `/api/games/{code}/join` needs no route change.
- **Frontend** (#7, #13): `PlayerSession.ensurePlayer()`, `PlayerNameDialog`, the auth interceptor, `GamesApi.createGame()`, `CreateGameDialog` with its component-scoped store, the `games/:gameCode` placeholder route. The hero's "Join an existing game" is still the link #7 left, and `games/join` still renders `ComingSoon`.
- **Next changes**: #17, #18 and #19 all update a stored game (ship placement, ready, shots, end). Two players act on the same game at once, so lost updates become real from here on.

## Goals / Non-Goals

**Goals:**
- One concurrency mechanism for every game update, small enough that later slices only supply "what to change".
- A join that is safe to repeat and safe to race.
- Refusals the frontend can tell apart without parsing text.

**Non-Goals:**
- Distributed locks, an outbox, or exactly-once event delivery.
- Removing a player or freeing the second seat.
- Real-time updates on the landing page or inside the join dialog.

## Decisions

### D1. Project layout additions (feature slices, ADR 0009)
```
src/Games/HexMaster.Battleship.Games.Abstractions/
  DataTransferObjects/JoinGameResponse.cs
  Repositories/IGameRepository.cs            (+ GetForUpdateAsync, TryUpdateAsync)
  Repositories/GameForUpdate.cs              (record: IGame Game, string ConcurrencyToken)
src/Games/HexMaster.Battleship.Games/
  DomainModels/Game.cs                       (+ Join, IsPlayer)
  Exceptions/GameJoinRefusedException.cs     (DomainException with a Reason)
  Services/IGameUpdater.cs, GameUpdater.cs, GameUpdateConflictException.cs
  Features/JoinGame/JoinGameCommand.cs, JoinGameCommandHandler.cs
  Endpoints/GamesEndpoints.cs                (+ POST /{code}/join)
src/Games/HexMaster.Battleship.Games.Data/
  Repositories/GameTableRepository.cs        (+ ETag read, conditional update)
```

### D2. Domain: `Game.Join`
`Game.Join(Guid playerId, string playerName, DateTimeOffset now)` returns a `JoinOutcome` (`Joined` or `AlreadyPlayer`) and checks in this order:
1. **Caller is player one or player two** → `AlreadyPlayer`, no mutation, no `Touch`. This comes first so a player can always get back into their own game, in any state.
2. **State is not `New`** → throws `GameJoinRefusedException(Reason.NotJoinable)`.
3. **Player two is taken** → throws `GameJoinRefusedException(Reason.GameFull)`.
4. Otherwise it creates player two with `HasTurn = false`, empty ships and shots, and calls `Touch(now, playerId)`. The state stays `New`.

`GameJoinRefusedException : DomainException` carries a `Reason` enum, so the endpoint maps it without string matching. `Game.IsPlayer(playerId)` is a small public query that #17's `GET` reuses for its "not a player → 404" rule.

- *Alternative: return a result type instead of throwing.* Rejected. #13 established `DomainException` for refused domain operations, and the turn rule already uses it.
- *Alternative: `AlreadyPlayer` as an exception too.* Rejected. It is a success path (`200`), and an exception would make the handler catch its own happy flow.

### D3. Optimistic concurrency (reused by #17, #18, #19)
**Repository contract** (`IGameRepository`, additions):
- `Task<GameForUpdate?> GetForUpdateAsync(GameCode code, CancellationToken ct)` returns the game and its ETag as an opaque `ConcurrencyToken` string.
- `Task<bool> TryUpdateAsync(IGame game, string concurrencyToken, CancellationToken ct)` writes the entity with `UpdateEntityAsync(entity, new ETag(token), TableUpdateMode.Replace)`. A `412 Precondition Failed` returns `false`. Any other failure throws.

The ETag stays a string in the abstraction, so `Games.Abstractions` keeps no Azure dependency.

**Updater** (`IGameUpdater`, in the module):
```csharp
Task<GameUpdateResult<T>> UpdateAsync<T>(
    GameCode code,
    Func<Game, GameMutation<T>> mutate,
    CancellationToken ct);
```
- `GameMutation<T>` is `Save(T value)` or `NoChange(T value)`. `mutate` runs the domain call and returns the value the handler needs, such as the outcome or the event data.
- For each attempt, up to `MaxAttempts = 3`:
  1. Load with `GetForUpdateAsync`. `null` gives `GameUpdateResult.NotFound`.
  2. Run `mutate` on the fresh game. A `DomainException` propagates unchanged; nothing was written.
  3. `NoChange` returns at once without writing.
  4. `Save` calls `TryUpdateAsync`. `true` returns the value. `false` records the attempt and loops.
- After three conflicts it throws `GameUpdateConflictException`. The endpoint maps it to `409` with problem type `game-update-conflict`.
- Retries are immediate, without backoff. Two human players produce very little contention, and a delay would only slow the loser of a race.
- Each attempt is an event on the caller's activity (`game.update.conflict`), and a counter `games.update.conflicts` counts conflicts.

Because `mutate` re-runs on fresh state, a re-applied change sees the winner's write (spec: "Re-applied change sees the new state"). In a race for the seat, the loser's second attempt finds player two taken and receives the game-full refusal.

- *Alternative: a lock row or a lease per game.* Rejected. It needs cleanup on crash, costs an extra round trip on every update, and table storage ETags already give compare-and-swap.
- *Alternative: let every handler write its own retry loop.* Rejected. Four changes would copy the same loop, and the bounded-retry rule would drift.
- *Alternative: `TableUpdateMode.Merge`.* Rejected. The document is one JSON column, so a merge offers no field-level benefit and would hide an inconsistent whole-document write.

### D4. Join slice and endpoint
- **Command.** `JoinGameCommand(GameCode Code, Guid PlayerId, string PlayerName)`. `JoinGameCommandHandler : ICommandHandler<JoinGameCommand, JoinGameResult>` does the following:
  1. Calls `IGameUpdater.UpdateAsync` with `game => game.Join(...) is Joined ? Save(snapshot) : NoChange(snapshot)`. The snapshot holds the game id and both player ids from the saved state.
  2. On `NotFound` it returns `JoinGameResult.NotFound`.
  3. On a saved join it sends `GameJoinedEvent(gameId, code, playerId, playerName)` with `EventAudience.Game(playerOne, playerTwo)` inside a `try`/`catch` that logs at `Error` with the game code and swallows, as in #13.
  4. It returns `JoinGameResult.Success(new JoinGameResponse(GameId, GameCode))`.
- **Endpoint.** `POST /games/{code}/join` in `GamesEndpoints`, `RequireAuthorization()`, no body, identity from `sub` and `name` exactly like `POST /games`.
  - The route value is upper-cased. If it fails `GameCode` validation, the endpoint answers `404` straight away (spec: "A code that does not match the game code format SHALL be treated as unknown").
- **Responses:**

| Outcome | Status | Body |
|---|---|---|
| Joined or already a player | `200` | `{ gameId, gameCode }` |
| Unknown or malformed code | `404` | problem details, type `game-not-found` |
| `Reason.NotJoinable` | `409` | problem details, type `game-already-started` |
| `Reason.GameFull` | `409` | problem details, type `game-full` |
| `GameUpdateConflictException` | `409` | problem details, type `game-update-conflict` |

- **Problem types.** Each type is a stable URI, `https://battleship.hexmaster.nl/problems/<slug>`, defined once in a `GameProblemTypes` static class in the module. The frontend keys on the trailing slug. This class is the place #17, #18 and #19 add their own types (`not-your-turn`, and so on).
- **Tracing.** The handler starts activity `games.join` tagged with `game.code` and `game.join.outcome` (`joined`, `already-player`, `full`, `not-joinable`, `not-found`). Counter `games.joined` is incremented on a saved join.

- *Alternative: `PUT /games/{code}/player-two`.* Rejected. The brief fixes `POST /games/{code}/join`, and joining is an action, not a resource the caller names.
- *Alternative: `403` for a full game.* Rejected. The caller is authorized; the game's state conflicts with the request, which is what `409` means.

### D5. Frontend join flow (implemented by the `angular-frontend` agent)
- **`games/games-api.ts`.** `GamesApi.joinGame(code: string): Promise<JoinGameResponse>` posts to `/api/games/${code}/join` with no body. A `JoinGameError` maps the response:
  - `404` → `'not-found'`;
  - `409` with problem type slug `game-full` → `'full'`;
  - `409` with slug `game-already-started` → `'started'`;
  - anything else, including `game-update-conflict` and network errors → `'failed'`.
- **`games/join-game-dialog/`.**
  - `JoinGameDialog` is an Angular Material dialog: title "Join an existing game", a game code `mat-form-field`, Cancel and "Join game" buttons, a progress indicator, and an inline error region (`role="alert"`).
  - The input upper-cases on input, strips characters outside `A-Z0-9`, limits to 8 characters, uses `autocapitalize="characters"`, `autocomplete="off"` and `spellcheck="false"`, and gets initial focus. "Join game" is enabled only for `^[A-Z0-9]{8}$`.
  - Its component-scoped `JoinGameStore` holds `{ code, status: 'idle' | 'joining' | 'failed', error: JoinGameError | null }` in one signal, exposed read-only. The intent `join()` ignores calls while `joining`. Editing the code clears the error.
  - Error messages: not found ("No game with code K7Q2M9XA was found."), full ("This game already has two players."), started ("This game has already started."), failed ("Something went wrong. Try again."). The code stays in the input after every error.
  - On success the dialog closes with `{ gameCode }`.
  - Styling uses the central SASS variables and the existing `bs-btn` classes; no literal colours or sizes.
- **Hero.** "Join an existing game" becomes a `<button type="button" class="bs-btn bs-btn--secondary">`. Its click handler awaits `ensurePlayer()`, plus `PlayerNameDialog` when needed, then opens `JoinGameDialog` and calls `router.navigate(['/games', gameCode])` on a result. This mirrors #13's create flow.
- **Routes.** `games/join` is removed. Old links fall through the wildcard to the landing page. `ComingSoon` stays only if something still uses it; otherwise it is deleted together with its spec.
- **Tests.** API error mapping with `HttpTestingController`; store and dialog specs (upper-casing, invalid code disables join, each error message, retry, double submit, cancel); hero specs for known, unknown-then-join, cancel with focus return and pending refresh.

- *Alternative: navigate to `/games/{code}` and let the game page join.* Rejected. Errors would then be shown on a page the player cannot use, and a simple page view would change server state.

### D6. Testing (backend)
- **`Game.Join`:** success, `AlreadyPlayer` for player one and for player two (also when `Started`), `NotJoinable` for `Started`, `Completed` and `Abandoned`, `GameFull`, and activity touched only on success.
- **`GameUpdater`** with a mocked repository:
  - not found;
  - `NoChange` writes nothing;
  - save on the first attempt;
  - a conflict, then success with `mutate` re-run on the reloaded game;
  - three conflicts throw `GameUpdateConflictException`;
  - a `DomainException` propagates without a write.
- **`JoinGameCommandHandler`:** the event and its `Game` audience with both ids; no event for `AlreadyPlayer`; a publish failure logged and swallowed.
- **Repository against Azurite:** `TryUpdateAsync` succeeds with a fresh token and returns `false` with a stale one. These tests are skipped with a clear reason when Azurite is unreachable, as in #13.
- **Endpoint (`WebApplicationFactory`, test-signed JWT):** `401`, `404` (unknown and malformed), `200`, the three `409` problem types, and lower-case code acceptance.
- **Race test:** two handlers run against an in-memory repository fake that honours tokens. The test asserts one success, one `GameFull`, and one event.
- **Coverage.** At least 80% on `HexMaster.Battleship.Games`.

## Risks / Trade-offs

- [This change depends on #13's code, which is not on `development` yet] → Task 1.1 gates on #13 being merged and stops otherwise. The deltas for `game-state` and `welcome-page` are written against #13's versions.
- [The ETag is not exposed by #13's `GetByCodeAsync`] → A separate `GetForUpdateAsync` keeps read-only callers unchanged. The implementer may fold the two together if #13's implementation already returns the ETag.
- [Three immediate retries can still lose under heavy contention] → They surface as `409 game-update-conflict`, which the frontend shows as a retryable error. Two players per game make this practically unreachable.
- [A swallowed publish failure means player one misses `game.joined.v1`] → It is logged with the game code. #17's page loads the game state on open and on refresh, so player one sees player two after a reload. An outbox remains a later change.
- [Idempotent re-join returns `200` for a started game the caller plays in] → This is intended: the dialog then takes the player back to their own game.
- [Problem type URIs become a contract with the frontend] → They are defined once in `GameProblemTypes`, and the endpoint tests assert the exact values.

## Migration Plan

Development only. No document shape change: `DocumentVersion` stays 1, and player two uses the shape #13 already defines. Existing `New` games become joinable without migration. Rollback is reverting the change.
