# Design

## Context

- Each game is a single row in the `games` table: PartitionKey = game code, RowKey = `game`, plus the columns `GameId`, `State`, `LastActivityOn`, `Document` and `DocumentVersion` (`GameTableRepository`, `GameEntity`). Deleting that row removes the game completely. No other store holds per-game state: the Events service keeps only in-memory buffers.
- `IGameRepository` can add, read and conditionally replace games. It cannot query or delete.
- Nothing in the solution runs on a schedule. Every API already runs with a Dapr sidecar (`AppHost.cs`), and the AppHost declares Dapr components with `AddDaprComponent`.
- The gateway routes only the prefixes declared in `GatewayRoutes.Modules`. `ReservedPrefixes` can never be declared, and `Validate` throws if one is. The `yarp-gateway-routes` skill owns that file.
- Use-case spans come from the Core handler decorator. Handlers add attributes to `Activity.Current`, and metrics and logs live in `Games/Observability`.

## Goals / Non-Goals

**Goals:**
- Delete inactive games with one query and one delete per game, without reading the JSON document.
- Never delete a game that becomes active again during the run.
- Make the trigger part of the local Aspire model, so `aspire run` shows the whole picture.

**Non-Goals:**
- A secondary index on activity. The filter is a cross-partition scan, evaluated server-side.
- Batching deletes. Every game has its own partition, so entity group transactions cannot combine them.
- Authentication on the maintenance endpoint. It is internal, like `/integration-events`.

## Decisions

### Trigger: Dapr cron input binding
- The AppHost adds `AddDaprComponent("game-cleanup", "bindings.cron")` with these metadata:
  - `schedule: @daily`
  - `route: /maintenance/game-cleanup`
  - `direction: input`
- The games-api sidecar references the component. The sidecar POSTs to the route on schedule.
- Alternatives considered:
  - A hosted `BackgroundService` with `PeriodicTimer`: runs once per replica and needs its own leader election once scaled out. It also hides the schedule inside the service.
  - An Aspire or Container Apps job: no local equivalent today.

  The cron binding reuses infrastructure every API already has, keeps the schedule declarative in the AppHost, and matches the issue's design of a background trigger calling an internal endpoint.
- The schedule comes from an AppHost parameter `game-cleanup-schedule`, default `@daily`. A developer can set `@every 1m` to try the cleanup locally.

### Endpoint: `POST /maintenance/game-cleanup`, no auth, reserved prefix
- `MaintenanceEndpoints.MapMaintenanceEndpoints()` maps a `/maintenance` group in the Games module, following the minimal-API endpoint organisation. `Program.cs` calls it.
- The endpoint dispatches `CleanUpInactiveGamesCommand` and returns `200` with `{ removed, skipped }`.
- `maintenance` is added to `GatewayRoutes.ReservedPrefixes`. The gateway already has no route for it, but reserving it stops anyone from declaring it later.
- Alternative: putting the endpoint under `/games/...`. Rejected, because the gateway's `games` catch-all would expose it.

### Selection: server-side filter, keys only
- `IGameRepository.GetInactiveAsync(DateTimeOffset cutoff, CancellationToken)` returns `IAsyncEnumerable<InactiveGame>`, where `InactiveGame(string GameCode, string ConcurrencyToken)`.
- The table implementation queries `LastActivityOn lt <cutoff>` with `select: [PartitionKey, RowKey]`. The ETag comes back with the entity, so the document is never transferred.
- The query pages through results with the SDK's `AsyncPageable`.
- Cutoff = `TimeProvider.GetUtcNow() - Retention`, compared strictly. "Older than 90 days" keeps a game that is exactly 90 days old.

### Deletion: conditional on the selected version
- `IGameRepository.TryDeleteAsync(string gameCode, string concurrencyToken, CancellationToken)` returns a `DeleteOutcome`: `Deleted`, `NotFound` or `Changed`.
- It deletes with the selected ETag:
  - `404` → `NotFound` (already gone)
  - `412` → `Changed` (touched since selection, so kept)
- Only `Deleted` counts as removed. The other two outcomes count as skipped.
- Alternative: an unconditional delete. Rejected, because it would remove a game that a player resumed seconds earlier.

### Configuration
- `GameRetentionOptions` is bound from `Games:Retention`, with `InactiveDays = 90`.
- It is validated on start: the value must be greater than 0.

### Observability
- The handler sets these span attributes:
  - `game.retention.cutoff`
  - `game.retention.removed`
  - `game.retention.skipped`
- `GamesMetrics` gains the counter `games.removed` (`{game}`).
- `GamesLog` gains event 4009 "Removed {Removed} inactive games (skipped {Skipped}) older than {Cutoff}". Codes are not logged per game, to avoid noise.

## Risks / Trade-offs

- **Full scan.** The filter visits every partition. → It is evaluated server-side and returns keys only. Revisit with an activity-bucket index if the table reaches the size where a daily scan costs noticeable time or RUs.
- **Long run blocks the binding call.** Dapr waits for the response. → The handler honours the request's cancellation token. Deletes are idempotent, so a run that is cut short is finished by the next one.
- **Several Games API replicas.** Each sidecar fires the cron. → Deletes are conditional and idempotent, so concurrent runs only waste work. Acceptable for now.
- **Internal endpoint without auth.** It is reachable by anything that can reach the API's port. → It is never routed by the gateway (reserved prefix), and the only effect is what the schedule would do anyway.

## Migration Plan

No data migration is needed: `LastActivityOn` already exists as a column on every stored game. Rolling back means removing the component and the endpoint. Deleted games cannot be restored, which is the intent.
