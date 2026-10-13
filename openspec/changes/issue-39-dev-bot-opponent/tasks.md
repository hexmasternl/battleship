# Tasks

## 1. Spike and scaffolding

- [x] 1.1 Spike the custom resource: in the AppHost, add a throw-away `BattleshipBotResource : ProjectResource` for an empty worker with `WithDaprSidecar`, `WithIconName("Bot")`, `ExcludeFromManifest()` and a property published through `ResourceNotificationService`. Verify with `aspire start` and `aspire describe` that the resource runs with its sidecar, shows the icon and keeps the custom property for 30 seconds. Record the outcome in `design.md` D2 and choose either the derived type or the parent and child fallback.
- [x] 1.2 Create `src/DevTools/HexMaster.Battleship.DevBot` (worker plus Minimal API host, net10.0, ServiceDefaults, references to `Games.Abstractions` and `IntegrationEvents`) and `src/DevTools/HexMaster.Battleship.DevBot.Tests` (xUnit, Moq, Bogus), and add both to `src/Battleship.slnx`. Verify that `dotnet build src/Battleship.slnx` succeeds and the empty test project runs.

## 2. Strategies and placement (no HTTP)

- [x] 2.1 Add `BotIntelligence` (four presets), `IShotStrategy` and the `Random` strategy. Verify with seeded tests that the strategy never repeats a cell and that after 143 shots it picks the last free cell.
- [x] 2.2 Add `HuntAndTarget` (unresolved hits, line extension). Verify with tests that a single hit leads to a neighbour, that two hits in a row lead to a cell extending the line, and that a hit whose neighbours are all shot returns to hunting.
- [x] 2.3 Add `HuntWithParity`. Verify with tests that hunting shots satisfy `(column + row) % 2 == 0`, that the strategy falls back when parity cells run out, and that target mode is the same as `HuntAndTarget`.
- [x] 2.4 Add `ProbabilityDensity` (placement counting with hit weighting). Verify with tests that on an empty board it prefers centre cells over corners, that it never picks a shot cell, and that after a hit it picks an adjacent cell.
- [ ] 2.5 Add `RandomFleet.Create(Random)`. Verify with tests that 1,000 seeded fleets all lie on the 12×12 board, never overlap, contain exactly the five `Fleet` kinds, and that two seeds give different fleets.
- [ ] 2.6 Add a strategy test that plays each preset to completion against 200 seeded random fleets and asserts the average shots to win order as Random > HuntAndTarget > HuntWithParity ≥ ProbabilityDensity. Verify that the test passes in under 10 seconds.

## 3. Identity and games client

- [ ] 3.1 Add a typed games client over the gateway (`https+http://gateway/api`) for tokens, join, get game, place ship, ready and (behind the #18 contract) shots, mapping problem types to results. Verify with `HttpMessageHandler` mock tests for each status code the specs list.
- [ ] 3.2 Add the token store and bearer `DelegatingHandler`: create the `Dev Bot` user at start, refresh one minute before expiry and once on `401`, and create a new user when a refresh fails. Verify with tests that use a fake clock.

## 4. Sessions, discovery and admin API

- [ ] 4.1 Add `GameSession` with per-game locking and `ReconcileAsync` (place the missing ships, ready, fire when it has the turn after the shot delay, end on `Completed` or `404`), and the session registry. Verify with tests over mocked client views for each branch, including that a `409` on a shot leads to a reconcile and not a retry.
- [ ] 4.2 Add the Dapr topic subscription endpoint, filtering by event type, target ids and freshness. A `game.created.v1` from another player schedules a join after the grace period when auto-join is on. Verify with tests for a stale envelope, the bot's own game, a human joining first (`409` full, dropped silently) and auto-join off.
- [ ] 4.3 Add the 15-second background reconcile timer. Verify with a fake-clock test that a session whose turn event was lost still fires.
- [ ] 4.4 Add `Endpoints/AdminEndpoints.cs` with `PUT /admin/intelligence`, `PUT /admin/auto-join`, `POST /admin/games/{code}` and `GET /admin/state`. An unknown preset answers `400` listing the four, and an unknown, full or started game answers with the matching problem detail. Exclude `/admin/state` from tracing. Verify with `WebApplicationFactory` endpoint tests.

## 5. Aspire custom resource and commands

- [ ] 5.1 Add `DevBot/BattleshipBotResource` and the `AddBattleshipBot(name)` extension (with `[ResourceName]`, icon, `ExcludeFromManifest`, health check, Dapr sidecar app id `dev-bot` with `pubsub`, `WithReference(gateway)` and `WaitFor(gateway)`), plus `WithIntelligence`, `WithJoinGracePeriod` and `WithShotDelay`, as chosen in 1.1. Wire the bot in `AppHost.cs` in run mode only. Remove the spike code. Verify with AppHost tests that the resource exists in run mode, is absent in publish mode, and that the `With…` methods set the expected `DevBot__*` environment variables.
- [ ] 5.2 Add the commands `set-intelligence` (choice input), `enable-auto-join`, `disable-auto-join` and `join-game` (code input with `ValidateArguments`), with `UpdateState` on health and the auto-join property, and the mapping from responses to results. Verify with unit tests of the handlers and `UpdateState` using a fake HTTP handler and snapshots.
- [ ] 5.3 Add the eventing subscriber that polls `GET /admin/state` and publishes the `devbot.intelligence`, `devbot.auto-join` and `devbot.games` properties. Verify with a test that a changed state is published, and manually with `aspire describe devbot`.
- [ ] 5.4 Document the bot: add a "Play against the dev bot" section to the README (commands, presets, grace period, how to resume after a restart) and a line in "Your first tour of the dashboard". Verify that the documented `aspire resource devbot set-intelligence --intelligence Random` (or the exact CLI form found in 5.2) works.

## 6. Play to the end (requires #18 and #19 merged into `development`)

- [ ] 6.1 Re-read the merged `game-play` and `game-ending` specs, align the shots client, `game.turn-changed.v1` and `game.ended.v1` handling and `winnerId` with them, and enable firing in `GameSession`. Verify that the client and session tests pass against the merged contracts.
- [ ] 6.2 Integration check: start the AppHost, create a game in the browser, let the bot join after the grace period, get ready, and play to the end with `ProbabilityDensity`, switching the preset mid-game. Verify in the dashboard that the properties update, that the game reaches `Completed`, and that the game code disappears from `devbot.games`.
- [ ] 6.3 Run the full `dotnet build` and `dotnet test` for `src/Battleship.slnx`, and verify that both pass.
