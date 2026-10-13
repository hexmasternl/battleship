# Proposal

> Tracks GitHub issue [hexmasternl/battleship#14](https://github.com/hexmasternl/battleship/issues/14).

## Why

The upcoming player and game features (#7, #13, #17) all need to announce what happened: a player was created, a game was joined, a shot was fired. Today the backbone (#3) and the delivery path to browsers (#4) exist, but there is no shared definition of those events and no way to publish one. Every feature would invent its own payload, type name and publishing code, and the pub/sub component name is already copied as the literal `"pubsub"` in four places. A single versioned catalog and one sender, wired in by default, fixes the contract before the first real event is published.

## What Changes

- New library `HexMaster.Battleship.IntegrationEvents` under `src/Core/`, added to `Battleship.slnx`, holding the integration event catalog:
  - `PlayerCreatedEvent`
  - `PlayerUpdatedEvent`
  - `GameCreatedEvent`
  - `GameJoinedEvent`
  - `GameStartedEvent`
  - `GameShotFiredEvent`
  - `GameEndedEvent`

  Each event has minimal payload fields and an explicit, versioned type name such as `player.created.v1`. A breaking change adds a new version next to the old one.
- An integration event sender, registered through dependency injection:
  - It wraps each event in the envelope from #4: id, type, target kind, target player ids, occurrence instant and payload.
  - It publishes the envelope through Dapr to topic `integration-events` on the shared `pubsub` component.
- One extension method, `AddIntegrationEvents()`, registers the catalog and the sender. `AddServiceDefaults()` calls it, so every backend service can publish. It takes over the Dapr client registration that `AddDaprMessaging()` does today, and **replaces** `AddDaprMessaging()`.
- A shared `AspireConstants` file in the ServiceDefaults project holds the pub/sub component name and the integration event topic name.
  - The AppHost compiles the file as a linked file, and so do the integration events library and the Events API.
  - The constants replace the `"pubsub"` literals in `AppHost.cs` and the defaults in the Events API `PubSubOptions`.
- **BREAKING** (development only): the throwaway smoke endpoints are removed (`/smoke/publish` in the Players API, `/smoke/received` in the Games API), together with their `smoke` topic subscription and the smoke integration test.
  - An AppHost integration test replaces the smoke test. It publishes a catalog event from the Players service and receives it on the Events service stream.
- The frontend list of known event types (`REALTIME_EVENT_TYPES`) is aligned with the catalog type names that are delivered to clients. Its current entries are placeholders that no publisher sends.

## Capabilities

### New Capabilities
- `integration-events`: the catalog of integration events the system publishes, their versioned type names and payloads, how each one is addressed (player, game or all), and how a backend service publishes one onto the shared topic.

### Modified Capabilities
- `local-messaging`: the end-to-end proof that an event crosses between services now uses a real integration event delivered to the Events service, instead of the `smoke` topic between the Players and Games APIs.

## Impact

- **New projects**:
  - `src/Core/HexMaster.Battleship.IntegrationEvents`.
  - `src/Core/HexMaster.Battleship.IntegrationEvents.Tests` (xUnit, Moq, Bogus).
  - Both are added to `src/Battleship.slnx` under a `/Core/` solution folder. The folder is created here if #7 has not created it yet.
- **ServiceDefaults** (`src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.ServiceDefaults`):
  - New `AspireConstants.cs` and a project reference to the integration events library.
  - `AddServiceDefaults()` calls `AddIntegrationEvents()`.
  - `DaprMessagingExtensions.cs` is removed.
- **AppHost**: links `AspireConstants.cs` and uses it in `AddDaprComponent`. No resource changes.
- **Events API**: `PubSubOptions` defaults come from the constants. The configuration section `Events:PubSub` still overrides them.
- **Games and Players APIs**: smoke endpoints and the `SmokeEventStore` registration removed. In `src/Games/HexMaster.Battleship.Games.Tests`, the smoke test and the Dapr messaging extension tests move to the new test project in their new form.
- **Frontend**: `src/Frontend/src/app/events/realtime-event-types.ts`.
- **Dependencies**: the integration events library references `Dapr.Client` (same version as the existing `Dapr.AspNetCore` 1.18.10) and `HexMaster.Battleship.Events.Abstractions` for the envelope.
- **Depends on**: #4 (merged to `development`). Archiving #4 first makes its `realtime-events` spec the baseline this change builds on.
- **Out of scope**:
  - Raising these events from features (#7, #13, #17 and later).
  - Subscribers other than the Events service.
  - A schema registry, or packaging the catalog as NuGet.
