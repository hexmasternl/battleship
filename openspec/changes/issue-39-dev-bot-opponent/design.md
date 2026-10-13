# Design

## Context

See `proposal.md` for the motivation, and `specs/dev-opponent/spec.md` for the behaviour.

What the code offers today (on `development`, plus #18 and #19 once merged):

- **Identity.** `POST /users/tokens` creates an anonymous user and returns an access token (15 minutes) and a refresh token. `POST /users/tokens/refresh` rotates them.
- **Games API.** Games are joined by code (`POST /games/{code}/join`). A game the caller is not in answers `404`, so there is no way to list or probe open games.
- **Ships and readiness.** `PUT /games/{code}/ships/{kind}` places one ship, and `POST /games/{code}/ready` gets the caller ready. Once both players are ready, the first turn is random.
- **Game state.** `GET /games/{code}` returns a `GameView`: the caller's ships and shots (with `hit`), whether the caller has the turn, and the opponent without ships. #19 adds `winnerId`.
- **Gateway.** The frontend calls the gateway under `/api/...`. `/users` and `/games` are routed.
- **Events.** Services publish integration-event envelopes on the Dapr `integration-events` topic (component `pubsub`, RabbitMQ). The Events API subscribes with `UseCloudEvents()`, `MapSubscribeHandler()` and `.WithTopic(...)`.
  - `game.created.v1` targets only the creator, so the SSE stream never shows it to anyone else.
  - Each Dapr app id gets its own durable queue on the topic.
- **The AppHost** runs on Aspire 13.6.1 with `CommunityToolkit.Aspire.Hosting.Dapr`. `AspireConstants` (pub/sub name, topic) is linked into the AppHost.
- **Board and fleet.** The board is 12×12 (`Board.Size`), and the fleet is Carrier 5, Battleship 4, Cruiser 3, Submarine 3, Destroyer 2 (`Fleet`). Both live in `Games.Abstractions`.
- **No sunk information.** The API does not say when a ship is sunk; only the end of the game is announced.

## Goals / Non-Goals

**Goals:**
- One self-contained worker that plays correctly against the public API. The server's game state is the truth, so missed or repeated events cannot desynchronise the bot.
- A typed Aspire custom resource with commands, following the two Aspire extensibility guides, with no change to any product service.
- Strategies that are pure, deterministic for a given seed, and unit-testable without HTTP.

**Non-Goals:**
- No product-facing bot, no lobby, and no LLM.
- No persistence of bot state across restarts.
- No inference of sunk ships beyond what hits and misses imply.

## Decisions

### D1. The bot is a separate process, not logic inside the AppHost
The Aspire guide's pattern of an eventing subscriber inside the AppHost was rejected:
- It has no Dapr sidecar, so it cannot see `game.created.v1`.
- It would mix game-client code into the hosting project.
- It loses per-resource logs and traces.

The worker uses ServiceDefaults, so its HTTP calls, Dapr deliveries and logs appear under `devbot` in the dashboard.

### D2. `BattleshipBotResource` derives from `ProjectResource`
`AddBattleshipBot("devbot")`:
- adds a `BattleshipBotResource : ProjectResource` with the `IProjectMetadata` annotation of the DevBot project, so DCP runs it like any project;
- applies `WithIconName("Bot")` and `ExcludeFromManifest()`;
- sets the HTTP health check and the Dapr sidecar (app id `dev-bot`, referencing `pubsub`);
- adds `WithReference(gateway)` and `WaitFor(gateway)`.

The `With…` extensions (`WithIntelligence`, `WithJoinGracePeriod`, `WithShotDelay`) are typed on `IResourceBuilder<BattleshipBotResource>`, so they only appear on the bot. They set `DevBot__*` environment variables.

**Spike first (task 1).** Confirm that a derived `ProjectResource` works with `WithDaprSidecar`, the dashboard and resource properties on 13.6.1.

**Fallback:** a plain `BattleshipBotResource : Resource` that owns the commands and properties, with a normal `AddProject` worker as its child (`WithParentRelationship`). The spec does not depend on which one is used.

### D3. Commands call internal admin endpoints on the worker
The AppHost commands are registered with `WithCommand`:

| Command | Arguments | Calls |
|---|---|---|
| `set-intelligence` | `InteractionInput` `intelligence`, `InputType.Choice`, the four presets, required | `PUT /admin/intelligence` |
| `enable-auto-join`, `disable-auto-join` | none | `PUT /admin/auto-join` |
| `join-game` | `InteractionInput` `code`, text, required, max length 8 | `POST /admin/games/{code}` |

How they behave:
- Each handler resolves the bot's `http` endpoint, sends the call, and maps `2xx` to `CommandResults.Success()` and a problem-details response to `CommandResults.Failure(detail)`.
- `UpdateState` requires `HealthStatus.Healthy`. The two auto-join commands also read the `devbot.auto-join` property from the snapshot to enable only the useful one.
- `ValidateArguments` checks the code format before the call.

The admin endpoints are mapped only on the worker. They are not in `GatewayRoutes`, so the browser cannot reach them.

The rejected alternative was changing environment variables and restarting the resource. That loses all games in progress and contradicts the "without a restart" requirement.

### D4. The AppHost mirrors the bot's state into resource properties
An eventing subscriber on the bot's `ResourceReadyEvent` polls `GET /admin/state` every 2 seconds, until the resource stops. That endpoint returns the preset, auto-join setting and active game codes. The subscriber publishes them with `ResourceNotificationService.PublishUpdateAsync` as the properties `devbot.intelligence`, `devbot.auto-join` and `devbot.games`.

Polling was chosen over a push channel from the worker to the AppHost because it needs no inbound endpoint in the AppHost and recovers by itself after a bot restart.

### D5. Game discovery through a Dapr topic subscription, with a freshness filter
The worker subscribes to the `integration-events` topic exactly like the Events API (`UseCloudEvents`, `MapSubscribeHandler`, `.WithTopic(pubsub, topic)`), under its own app id. It therefore gets its own queue and never competes with the Events API. Every delivery is acknowledged.

The worker reacts to:
- `game.created.v1` from any creator except itself;
- `game.joined.v1`, `game.player-ready.v1`, `game.started.v1`, `game.turn-changed.v1` and `game.ended.v1` whose target ids include the bot's user id.

The queue is durable, so after a stop the bot would receive old `game.created.v1` envelopes. Envelopes older than the grace period plus 60 seconds are therefore ignored.

The rejected alternatives:
- the SSE stream, which never carries `game.created.v1` for the bot;
- a new open-games endpoint, which changes the product contract.

### D6. One session per game, reconciled from `GET /games/{code}`
Each game the bot plays is a `GameSession` with its own lock. Every event or command for the game triggers `ReconcileAsync`:
1. Read `GET /games/{code}`.
2. Act on the view:
   - fewer than five ships: place the missing ones;
   - not ready: `POST ready`;
   - `Started` and `me.hasTurn`: wait for the shot delay, choose a cell, `POST shots`;
   - `Completed`, or `404`: end the session.

Events are only triggers; the server view decides. Duplicate, out-of-order or missed events are therefore harmless.

A background timer reconciles every active session every 15 seconds as a safety net. A `409` on a shot (not your turn, already shot) triggers a reconcile instead of a retry loop.

Joining after the grace period is a plain `POST join`:
- `409 game-full` or `game-already-started` drops the candidate silently (debug log);
- `404` is ignored.

### D7. Strategies are pure functions over the bot's own shots
```csharp
interface IShotStrategy { Coordinate NextShot(IReadOnlyList<ShotView> myShots, Random random); }
```
`BotIntelligence` maps to an implementation. The active preset is read on every shot, so a change applies from the next shot.

Without sunk information, the strategies work as follows:
- **HuntAndTarget.** "Unresolved hits" are hits with an unshot orthogonal neighbour. When two or more unresolved hits are in a line, cells extending that line come first. Otherwise any unshot neighbour is used. With no unresolved hit, it fires at a random unshot cell.
- **HuntWithParity.** Same as HuntAndTarget, but the hunt draws only from cells where `(column + row) % 2 == 0`. It falls back to all cells when those run out.
- **ProbabilityDensity.** For each of the five ship lengths, it enumerates every horizontal and vertical placement that covers no miss. Each placement adds 1 to its unshot cells, or 20 when it covers at least one hit, to steer towards targeting. It picks the highest-scoring unshot cell, with ties broken by `random`. All five lengths are always counted, because sunk ships are unknown.
- **Random.** Any unshot cell.

Each session gets its own `Random`, seeded from the game code hash plus the start time. Tests inject a fixed seed.

### D8. Fleet placement is local and random
`RandomFleet.Create(Random)` places the ships longest first. Each ship gets a random orientation and an origin drawn from the positions that fit on the board and do not overlap, restarting on a dead end. The five `PUT` calls follow. A `400` overlap or out-of-board response, which should not happen, triggers a full re-placement once and is then logged as an error.

### D9. Identity and token refresh
At start, the worker calls `POST /api/users/tokens` with display name `Dev Bot` and keeps the token pair in memory.

A `DelegatingHandler` adds the bearer token. It refreshes the pair one minute before the access token expires, and once on any `401`. If a refresh fails, it creates a new user (and logs a warning); the games of the old identity are then lost.

The gateway base address comes from service discovery (`WithReference(gateway)`), and requests go to `https+http://gateway/api/...`.

### D10. Location and project shape
The code goes in `src/DevTools/HexMaster.Battleship.DevBot`, with `HexMaster.Battleship.DevBot.Tests`. It is dev tooling, not a module: it has no `Abstractions` or data project and owns no data.

Inside, the code is organised by concern: `Identity/`, `Events/`, `Sessions/`, `Strategies/`, `Placement/` and `Admin/`. The admin endpoints follow the Minimal API endpoint-file convention (`Endpoints/AdminEndpoints.cs`).

It references:
- `Games.Abstractions` for `Board`, `Fleet`, `Coordinate`, `ShipKind`, `Orientation` and `GameView`;
- `IntegrationEvents` for event type names and payload records;
- ServiceDefaults.

## Risks / Trade-offs

- **[Risk] DCP overwrites custom properties on a project-backed resource.** → The spike checks this. If it happens, the D2 fallback is used, where the parent resource's snapshot is fully owned by the AppHost.
- **[Risk] Durable queue replays stale `game.created.v1`.** → The freshness filter in D5.
- **[Risk] The bot takes games meant for two humans.** → The grace period and the auto-join switch. Two humans normally join within seconds.
- **[Trade-off] Polling for dashboard state** adds a request every 2 seconds locally. → It is negligible, and the request is excluded from health and tracing noise by filtering `/admin/state` in the ServiceDefaults trace filter.
- **[Trade-off] No sunk information** makes `ProbabilityDensity` keep counting sunk ships. → It is still clearly stronger than the other presets, and good enough for a dev opponent.
- **[Risk] #18 and #19 change their contracts before merging.** → The play tasks are grouped last and re-read the merged specs before starting.

## Migration Plan

This is purely additive and local. Rollback means removing the `AddBattleshipBot` call from `AppHost.cs`. No data migrations are involved. The bot's Dapr queue (`dev-bot-integration-events`) can be deleted from the RabbitMQ management UI if the bot is removed.
