# Design

## Context

See proposal.md (Why). Current state that shapes the approach:

- **Envelope contract.** #4 owns the envelope and its validation:
  - `IntegrationEventEnvelope(Id, Type, Target, TargetIds, OccurredAt, JsonElement Payload)`, `EventTarget` (`player`/`game`/`all`) and `IntegrationEventEnvelopeValidator`, in `src/Events/HexMaster.Battleship.Events.Abstractions`.
  - The Events API subscribes `POST /integration-events` to `PubSubOptions.Name`/`Topic` (defaults `"pubsub"` / `"integration-events"`, overridable via `Events:PubSub`). It drops invalid envelopes with `{"status":"DROP"}`.
- **Dapr registration.** `AddServiceDefaults()` calls `AddDaprMessaging()`, which only does `AddDaprClient()`. ServiceDefaults is an `IsAspireSharedProject` and every API references it. The AppHost references only the API projects.
- **Hard-coded names.** `"pubsub"` is a literal in `AppHost.cs`, the two `SmokeEndpoints.cs` files, the smoke test and the `PubSubOptions` default.
- **No Core yet.** `src/Core` does not exist on `development`. #7 plans `src/Core/HexMaster.Battleship.Core` (CQRS interfaces, domain base types), but that change is not implemented yet.
- **Frontend event names.** The frontend registers `EventSource` listeners only for names in `REALTIME_EVENT_TYPES`. Today those are the placeholders `shot-fired`, `ships-placed`, `game-started` and `turn-changed`.

## Goals / Non-Goals

**Goals:**
- One place that defines every integration event and its wire name.
- One sender every service gets for free, and that can only produce envelopes the Events service accepts.
- The pub/sub component and topic names declared once and shared by the AppHost, the sender and the Events subscription.

**Non-Goals:**
- Publishing events from any feature handler.
- Consuming catalog events in the Games or Players services.
- Outbox, retries beyond what Dapr and RabbitMQ already provide, or transactional publishing.
- Deserialising catalog events on the subscriber side. The Events service keeps treating the payload as opaque JSON.

## Decisions

### D1. Library location and dependencies
`src/Core/HexMaster.Battleship.IntegrationEvents` (namespace `HexMaster.Battleship.IntegrationEvents`):
- It references `HexMaster.Battleship.Events.Abstractions` for the envelope, `EventTarget` and the validator.
- It references `Dapr.Client` 1.18.10, matching the `Dapr.AspNetCore` version ServiceDefaults already uses.
- ServiceDefaults references the library.

This follows the modular-monolith rule that cross-module references go only to an Abstractions project. The library is shared infrastructure, like ServiceDefaults, not a module.

- *Alternative: put the catalog in `Events.Abstractions`.* Rejected. Those abstractions describe the Events module's ingestion contract. Making them the home of Players and Games domain events would turn a module's abstractions into a cross-module dumping ground, and the Events service would compile against every game event although it never reads payloads.
- *Alternative: move the envelope into the new library.* Rejected for now. It is an unnecessary churn of #4's freshly merged code, and the dependency direction (catalog → envelope contract) is already correct.

### D2. Event shape and versioning
- Each event is a `sealed record` implementing `IIntegrationEvent`, which has a `static abstract string EventType { get; }`. Example: `public sealed record PlayerCreatedEvent(Guid PlayerId, string PlayerName) : IIntegrationEvent { public static string EventType => "player.created.v1"; }`.
- Records sit in folders per subject (`Players/`, `Games/`). The first version has no suffix in its C# name. A breaking change adds `PlayerCreatedEventV2` with `player.created.v2` next to it.
- `IntegrationEventCatalog` exposes the list of all event types and their type names. Registration tests use it, and it is the one place to look up what exists.
- A unit test enforces three things: every type name matches `^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*\.v[1-9][0-9]*$`, no type name repeats, and every `IIntegrationEvent` implementation in the assembly is listed in the catalog.

Why a static abstract member: the generic sender can read the type name at compile time, with no reflection and no attribute lookup. Forgetting it is a compile error.

- *Alternative: attributes or a naming convention derived from the class name.* Rejected. Convention silently changes the wire name on a rename, which is exactly the versioning hazard the issue cares about.

Payload fields follow the spec. Ids are `Guid`; `gameCode` is a `string`. `GameEndedEvent.Reason` is a string enum, `GameEndReason` (`completed`, `abandoned-by-player`, `ended-by-server`), using the same camelCase/kebab JSON converter pattern as `EventTarget`. `WinnerId` is `Guid?`.

### D3. Sender API and audience
```csharp
public interface IIntegrationEventSender
{
    Task SendAsync<TEvent>(TEvent integrationEvent, EventAudience audience, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
```
- `EventAudience` is a small value type with three factories: `EventAudience.Players(params IEnumerable<Guid> ids)`, `EventAudience.Game(params IEnumerable<Guid> playerIds)` and `EventAudience.Everyone`. It maps to `EventTarget` and `TargetIds` (ids as strings, the envelope's format).
- `DaprIntegrationEventSender` does the following:
  1. Builds the envelope with `Id = Guid.NewGuid()`, `OccurredAt = TimeProvider.GetUtcNow()` and `Payload = JsonSerializer.SerializeToElement(event, web defaults)`.
  2. Runs `IntegrationEventEnvelopeValidator` and throws `ArgumentException` on any error.
  3. Calls `DaprClient.PublishEventAsync(AspireConstants.PubSubName, AspireConstants.IntegrationEventsTopic, envelope, ct)`.

Validating with #4's validator means the sender can never publish an envelope the Events service would drop.

The audience is a parameter, not something each event declares, because the same event can go to different audiences. For example, game ended goes to both players when a player resigns, and to the remaining player when the server abandons a game. The publisher knows the context; the catalog does not.

`TimeProvider` is registered with `TryAddSingleton(TimeProvider.System)`, so tests can supply a fake.

- *Alternative: the sender targets everyone by default.* Rejected. It would leak game events to all players.

### D4. `AddIntegrationEvents()` replaces `AddDaprMessaging()`
- `AddIntegrationEvents<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder` lives in the library under namespace `Microsoft.Extensions.Hosting`, like the other defaults.
- It calls `AddDaprClient()` and registers `IIntegrationEventSender` as a singleton (`DaprClient` is a singleton and the sender is stateless) and `TimeProvider`.
- `AddServiceDefaults()` calls it where `AddDaprMessaging()` was. `DaprMessagingExtensions.cs` is deleted, so `DaprClient` stays registered once.

The test-project shape follows the unit-testing standard: xUnit, Moq and Bogus, with factories under `Factories/`. The existing `DaprMessagingExtensionsTests` move to the new test project as `IntegrationEventsRegistrationTests`. The Games test project no longer needs them, because it never owned ServiceDefaults.

### D5. `AspireConstants` as an internal linked file
- `AspireConstants.cs` lives in the ServiceDefaults project as `internal static class AspireConstants` in namespace `HexMaster.Battleship.Aspire`, with `PubSubName = "pubsub"` and `IntegrationEventsTopic = "integration-events"`.
- It is compiled into each project that needs it through `<Compile Include="..\..\..\Aspire\...\AspireConstants.cs" Link="AspireConstants.cs" />`:
  - the AppHost (as the issue asks);
  - the integration events library;
  - the Events API (`PubSubOptions` defaults).

Why `internal`: compiling the same `public` type into several assemblies, where a consumer references more than one of them, produces conflicting type definitions (CS0433/CS0436). As an internal class, each assembly gets a private copy of the same source with no conflict, and there is still one file to edit.

- *Alternative: the library exposes the constants publicly and the AppHost references the library.* Rejected. The AppHost would then depend on Dapr.Client and the Events abstractions just to read two strings, and the issue explicitly asks for a linked file.
- *Alternative: the library references ServiceDefaults for the constants.* Rejected. It creates a cycle, because ServiceDefaults must reference the library to call `AddIntegrationEvents()`.

`Events:PubSub` configuration stays as an override on the subscriber side. Its defaults become the constants.

### D6. Replacing the smoke round trip
- Remove the smoke code from the Players and Games APIs:
  - `SmokeEndpoints.cs` and `MapSmokeEndpoints()` in both;
  - the `SmokeEventStore` registration;
  - the Games `Topic("pubsub", "smoke")` subscription.
- Delete `SmokeMessagingTests`.
- The new `IntegrationEventsDeliveryTests` in `IntegrationEvents.Tests/Integration` has `[Trait("Category", "Integration")]`, because it needs Docker and Dapr like the smoke test. It does the following:
  1. Starts the AppHost and waits for these resources to be healthy: `messaging`, the Events API, the Players API and their sidecars.
  2. Waits for the Events sidecar's metadata to list the `integration-events` subscription.
  3. Opens `GET /events?playerId=<A>` on the Events API.
  4. Builds a `DaprClient` against the Players sidecar's HTTP endpoint (`app.GetEndpoint(PlayersSidecar, "http")`) and calls the real `DaprIntegrationEventSender` with a `PlayerCreatedEvent` addressed to A.
  5. Asserts that A's stream receives `event: player.created.v1` with the payload within five seconds.

This proves the modified `local-messaging` scenario and the spec's "envelope reaches the Events service" scenario with production code on both ends. It does not keep a test-only endpoint in a real service.

### D7. Frontend event names
`REALTIME_EVENT_TYPES` becomes the catalog names that are delivered to clients:

```text
player.created.v1   player.updated.v1   game.created.v1   game.joined.v1
game.started.v1     game.shot-fired.v1  game.ended.v1
```

The placeholders `shot-fired`, `ships-placed`, `game-started` and `turn-changed` have no publisher and are removed. The file's comment changes from "publishers that reference Events.Abstractions" to "the catalog in `HexMaster.Battleship.IntegrationEvents`". Existing frontend specs (`events.service.spec.ts`) that use `shot-fired` as sample data switch to a catalog name. No behaviour change.

## Risks / Trade-offs

- [The linked internal constants could drift if someone defines a second copy.] → The registration test asserts that the sender publishes to the same name as the AppHost component. The integration test fails end to end if the names diverge.
- [Removing the smoke endpoints removes the quickest manual check of the broker.] → The new integration test covers the same path. The RabbitMQ management UI remains available for manual checks.
- [`src/Core` is created here and also by #7, which can cause slnx and folder merge conflicts.] → Whichever change lands second rebases on `development`. The conflict is limited to one `<Folder Name="/Core/">` element in `Battleship.slnx`.
- [Payloads are minimal and will grow.] → Adding optional fields needs no new version (spec). Only incompatible changes add a version.
- [The frontend name list is still kept by hand.] → It is now derived from one catalog with a documented rule. Generating it is deferred.

## Migration Plan

Development only; there is no deployed environment.
1. Land the library, the constants and the sender.
2. Remove the smoke code in the same change.

Rollback is a revert of the change. No data or configuration migration is needed. Local overrides of `Events:PubSub` keep working.

## Open Questions

- Should the frontend list of event names be generated from the catalog at build time? This can be decided later without changing the specs or this approach.
