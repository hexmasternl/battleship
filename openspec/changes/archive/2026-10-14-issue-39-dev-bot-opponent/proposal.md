# Proposal

> Tracks GitHub issue [hexmasternl/battleship#39](https://github.com/hexmasternl/battleship/issues/39).

## Why

To try a game end to end today, a developer has to run two browsers and play both sides by hand. Placing ten ships and alternating every shot makes testing gameplay, the event stream and game end slow and tedious. A bot opponent that runs with the local Aspire environment lets one developer play a full game alone. A playing strength that can be changed from the dashboard makes it useful for quick smoke tests and for real games.

## What Changes

- **New dev bot worker.** `src/DevTools/HexMaster.Battleship.DevBot`, a .NET worker that plays Battleship through the same public API the frontend uses (through the gateway), with ServiceDefaults telemetry. It:
  - gets its own anonymous user (`Dev Bot`) and keeps its access token fresh;
  - learns about new games from `game.created.v1` on the `integration-events` topic through its own Dapr sidecar. This is the only place that event is visible to anyone but the creator;
  - joins a new game when no second player has joined within a grace period (default 10 seconds), so two humans testing in two browsers can still take the seat;
  - places a valid random fleet, gets ready, waits for the human, fires on its turns with the active strategy, and stops when the game ends;
  - plays any number of games at once.
- **Four intelligence presets** as deterministic shot strategies, with no LLM: `Random`, `HuntAndTarget`, `HuntWithParity` and `ProbabilityDensity`. A preset change applies from the bot's next shot in every game.
- **Custom Aspire resource.** In the AppHost:
  - a typed `BattleshipBotResource` with the `AddBattleshipBot(...)` extension, following the Aspire custom-resources guide;
  - run mode only, excluded from the manifest, with a bot icon;
  - the current preset, auto-join setting and games in play shown as resource properties.
- **Custom resource commands** on the bot, following the Aspire custom-resource-commands guide:
  - *Set intelligence*: a choice input with the four presets, also usable from the Aspire CLI;
  - *Enable auto-join* and *Disable auto-join*;
  - *Join game*: a game code input. It also resumes a game the bot already plays in, after a restart.
- **Docs.** The README gains a "Play against the dev bot" section, and the dashboard tour mentions the bot resource.

## Capabilities

### New Capabilities
- `dev-opponent`: the local development bot opponent. Covers:
  - its presence in the local environment only;
  - its identity;
  - finding and joining games, including the grace period and the auto-join switch;
  - fleet placement and getting ready;
  - taking turns until the game ends;
  - the four intelligence presets and how a change applies;
  - the dashboard commands and the state the bot shows.

### Modified Capabilities

None. The bot is a client of the existing `game-joining`, `fleet-placement`, `game-view` and `integration-events` contracts, and of the `game-play` and `game-ending` contracts that #18 and #19 add. It changes none of them. The `local-gateway` route table already covers `/users` and `/games`.

## Impact

- **New projects**:
  - `src/DevTools/HexMaster.Battleship.DevBot`: the worker, its strategies and its internal admin endpoints;
  - `src/DevTools/HexMaster.Battleship.DevBot.Tests`: xUnit, Moq and Bogus.
  Both are added to `src/Battleship.slnx`.
- **AppHost** (`src/Aspire/.../AppHost`): a `DevBot/` folder with `BattleshipBotResource`, the `AddBattleshipBot` and `With…` extensions, and the command handlers. `AppHost.cs` adds the bot in run mode. AppHost tests go in `HexMaster.Battleship.Aspire.Tests`.
- **Dependencies**: no new NuGet packages beyond those already used. The worker reuses `HexMaster.Battleship.Games.Abstractions` for `Fleet`, `Board`, `ShipKind`, `Coordinate` and `GameView`, and `HexMaster.Battleship.IntegrationEvents` for event types.
- **Gateway**: no route change. The bot's admin endpoints are not routed and are reached only by the AppHost.
- **Games, Users, Events services and frontend**: unchanged.
- **Depends on** (must be merged before the play tasks): #18 `issue-18-game-play-page` for `POST /games/{code}/shots` and `game.turn-changed.v1`, and #19 `issue-19-game-end` for `game.ended.v1` and `winnerId`. The resource, commands, identity, joining, placement and strategy tasks can be done before them.
- **Out of scope**:
  - LLM-driven play, chat or trash talk;
  - a "play against bot" option in the product;
  - a lobby or open-games endpoint;
  - persisting the preset or the bot's games across AppHost restarts;
  - running the bot outside the local environment;
  - knowledge of sunk ships, which the API does not expose.
- **Assumed** (answers to the open questions on #39, chosen so the change is implementable):
  - auto-join is on by default, with a 10-second grace period;
  - a preset change applies immediately, from the next shot;
  - the preset is not persisted, and every run starts from the preset configured in `AppHost.cs` (default `HuntAndTarget`);
  - after a restart, games in progress are not resumed automatically, but *Join game* with the code resumes one;
  - the bot waits 1 second before each shot so its moves are visible;
  - the code lives under `src/DevTools/` as dev tooling, not as a module.
