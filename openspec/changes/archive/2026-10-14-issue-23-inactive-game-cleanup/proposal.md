# Proposal

> Tracks GitHub issue [hexmasternl/battleship#23](https://github.com/hexmasternl/battleship/issues/23).

## Why

Games are never removed today, so the `games` table grows without limit and keeps player data for games nobody returns to. A daily cleanup that removes games with no activity for 90 days bounds storage and retention without anyone having to act.

## What Changes

- The Games service gains an internal cleanup operation. It deletes every stored game whose last activity is older than the retention period: 90 days by default, configurable. The game's state does not matter.
- Candidates are selected by a server-side filter on the existing last-activity column. The game document is never read or deserialized.
- The cleanup is safe to repeat:
  - A game that is already gone is skipped.
  - A game that gets new activity between selection and deletion is kept.
- The operation is exposed on an internal endpoint under a new `maintenance` prefix. That prefix is reserved in the gateway route table, so the gateway never routes it.
- The Aspire AppHost adds a scheduled trigger that calls the cleanup endpoint once a day: a Dapr cron input binding on the Games API sidecar.
- Each run is traced as a use case. It emits a deleted-games metric and a structured log line with the counts.

## Capabilities

### New Capabilities
- `game-retention`: how long the Games service keeps inactive games, how and when they are removed, and how the removal is triggered and observed.

### Modified Capabilities
- `local-gateway`: the gateway's reserved, never-routed prefixes gain `maintenance`, used for internal maintenance endpoints of the module APIs.

## Impact

- **Games**:
  - `IGameRepository` gains a query for inactive games and a conditional delete.
  - `GameTableRepository` implements both, and the `InMemoryGameRepository` test fake follows.
  - A new `CleanUpInactiveGames` feature slice (command, handler, options), a `MaintenanceEndpoints` group mapped in `Games.Api/Program.cs`, and new metric and log entries in `Observability/`.
- **Aspire**:
  - `AppHost.cs` adds a `bindings.cron` Dapr component referenced by the games-api sidecar.
  - `GatewayRoutes.cs` reserves `maintenance`, with a test in `Aspire.Tests`. This is owned by the `yarp-gateway-routes` skill and approved there.
- **Out of scope**:
  - Notifying players or publishing integration events for removed games.
  - Cleaning up Users data.
  - Cloud or production scheduling infrastructure.
  - A secondary activity index. At the current scale a cross-partition server-side filter is accepted.
