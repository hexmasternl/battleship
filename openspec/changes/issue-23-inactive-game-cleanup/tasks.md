# Tasks

## 1. Repository: query inactive games and conditional delete

- [x] 1.1 Add `InactiveGame(string GameCode, string ConcurrencyToken)` and `DeleteOutcome { Deleted, NotFound, Changed }` to `Games.Abstractions/Repositories`. Add `GetInactiveAsync(DateTimeOffset cutoff, CancellationToken)` (returns `IAsyncEnumerable<InactiveGame>`) and `TryDeleteAsync(string gameCode, string concurrencyToken, CancellationToken)` to `IGameRepository`. Verify the solution builds.
- [x] 1.2 Implement both in `GameTableRepository`:
  - Query with a filter on `LastActivityOn` less than the cutoff and a select of the keys only.
  - Delete with the ETag; map 404 to `NotFound` and 412 to `Changed`.

  Verify with new `GameTableRepositoryTests` against Azurite:
  - 89 days stays, 91 days is returned.
  - A row with a corrupt `Document` is still returned and deleted.
  - Deleting twice gives `Deleted` and then `NotFound`.
  - An update between selection and delete gives `Changed` and the row survives.
- [x] 1.3 Implement both members in `Tests/Fakes/InMemoryGameRepository.cs` with the same semantics. Verify the existing Games tests still pass.

## 2. Cleanup feature slice

- [x] 2.1 Add `GameRetentionOptions` (`Games:Retention:InactiveDays`, default 90, validated greater than 0 on start). Register it in `AddGamesModule`. Verify with a unit test that a value of 0 fails validation.
- [x] 2.2 Add `games.removed` to `GamesMetrics`, the `game.retention.*` attributes to `GamesTelemetry` and event 4012 to `GamesLog` (4009 is taken by `ShotFired`). Verify the build, and in 2.3 that the metric moves.
- [x] 2.3 Add `Features/CleanUpInactiveGames`:
  - `CleanUpInactiveGamesCommand` and a `CleanUpInactiveGamesResult(int Removed, int Skipped)` DTO in Abstractions.
  - A handler that computes the cutoff from `TimeProvider`, iterates `GetInactiveAsync`, calls `TryDeleteAsync`, counts the outcomes, and sets span attributes, the metric and the log.
  - Register it with `AddCommandHandler`.

  Verify with `CleanUpInactiveGamesCommandHandlerTests` (fake repository, `FakeTimeProvider`, Bogus `GameFactory`):
  - The 89/91-day boundary.
  - All four states are removed.
  - A configured 30-day retention.
  - A second run removes 0.
  - A `Changed` outcome counts as skipped.
  - `games.removed` increases by the removed count.

## 3. Internal endpoint

- [x] 3.1 Add `Endpoints/MaintenanceEndpoints.cs` with `MapMaintenanceEndpoints()`: a `/maintenance` group and `POST /game-cleanup` without authorization, dispatching the command and returning `200` with `{ removed, skipped }`. Call it from `Games.Api/Program.cs`. Verify with an integration test in `Tests/Integration` (WebApplicationFactory with the in-memory repository): seed one old and one recent game, POST without a token, and expect `200` with `removed: 1`. A follow-up `GET /games/{code}` for the old game then answers `404`.

## 4. Aspire: reserved prefix and daily trigger

- [x] 4.1 Load the `yarp-gateway-routes` skill and get approval to reserve `maintenance`. Add it to `GatewayRoutes.ReservedPrefixes`. Verify with a `GatewayRoutesTests` case where declaring `maintenance` for a module throws and names the module and the prefix.
- [x] 4.2 In `AppHost.cs`:
  - Add the AppHost parameter `game-cleanup-schedule` (default `@daily`).
  - Add a `bindings.cron` Dapr component `game-cleanup` with metadata `schedule` (from the parameter), `route` `/maintenance/game-cleanup` and `direction` `input`.
  - Reference it from the games-api sidecar.

  Verify the AppHost builds and `GatewayRoutesTests` and the other Aspire tests pass.
- [ ] 4.3 Run the AppHost with the schedule set to `@every 1m` and a game whose `LastActivityOn` was edited in Azurite to 100 days ago. Verify that within a minute:
  - the game row is gone;
  - the dashboard shows the cleanup use-case span under the sidecar's binding span, and `games.removed` increases;
  - `POST <gateway>/maintenance/game-cleanup` answers `404`.

## 5. Integration check

- [ ] 5.1 Run `dotnet build src/Battleship.slnx` and `dotnet test src/Battleship.slnx` from the worktree. Verify both are green and Games test coverage stays at least 80%.
