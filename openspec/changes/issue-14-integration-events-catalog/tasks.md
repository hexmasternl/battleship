# Tasks

## 1. Projects and shared constants

- [x] 1.1 Create `src/Core/HexMaster.Battleship.IntegrationEvents` (net10.0, nullable, implicit usings, `FrameworkReference Microsoft.AspNetCore.App` for hosting abstractions) referencing `Events.Abstractions` and `Dapr.Client` 1.18.10, and `src/Core/HexMaster.Battleship.IntegrationEvents.Tests` (xUnit, Moq, Bogus, coverlet, `Aspire.Hosting.Testing`, references to the library, ServiceDefaults and the AppHost); add both under a `/Core/` folder in `src/Battleship.slnx` (create the folder if missing); verify `dotnet build src/Battleship.slnx` succeeds
- [x] 1.2 Add `AspireConstants.cs` (`internal static class` with `PubSubName = "pubsub"` and `IntegrationEventsTopic = "integration-events"`) to the ServiceDefaults project and link it with `<Compile Include=... Link="AspireConstants.cs" />` into the AppHost, the integration events library and the Events API (design D5); verify the solution builds with no CS0433/CS0436 warnings
- [x] 1.3 Use the constants in `AppHost.cs` (`AddDaprComponent(AspireConstants.PubSubName, ...)`) and as the defaults of `PubSubOptions.Name`/`Topic` in the Events API; verify `grep -rn '"pubsub"' src --include=*.cs` only matches `AspireConstants.cs` and test assertions, and the existing Events tests still pass (`dotnet test src/Events/HexMaster.Battleship.Events.Tests`)

## 2. Event catalog

- [x] 2.1 Add `IIntegrationEvent` (static abstract `EventType`) and the seven v1 records under `Players/` and `Games/` with the payload fields from the spec, plus `GameEndReason` with its kebab-case JSON converter (design D2); add `IntegrationEventCatalog` listing every event type; verify with unit tests that type names match the format, are unique, and that every `IIntegrationEvent` in the assembly is in the catalog
- [x] 2.2 Add serialization tests with Bogus-built events (factories under `Factories/`) asserting camelCase payload JSON for each event, `gameCode`/`column`/`row` for shot fired, `reason` and absent `winnerId` for a server-ended game; verify `dotnet test src/Core/HexMaster.Battleship.IntegrationEvents.Tests` passes
- [x] 2.3 Document the catalog and the versioning rule (new version alongside, optional fields need no new version) in a `README.md` in the library folder; verify it lists all seven type names exactly as `IntegrationEventCatalog` does

## 3. Sender and registration

- [x] 3.1 Add `EventAudience` (`Players`, `Game`, `Everyone`) mapping to `EventTarget` and string `TargetIds`, and `IIntegrationEventSender` with `DaprIntegrationEventSender` building the envelope (new id, `TimeProvider` UTC instant, serialized payload), validating it with `IntegrationEventEnvelopeValidator`, throwing `ArgumentException` on errors and publishing via `DaprClient.PublishEventAsync(AspireConstants.PubSubName, AspireConstants.IntegrationEventsTopic, ...)` (design D3); verify with Moq-based unit tests: envelope type/target/ids/instant/payload, distinct ids on repeated sends, empty player audience throws and never calls `DaprClient`
- [ ] 3.2 Add `AddIntegrationEvents()` (namespace `Microsoft.Extensions.Hosting`) registering `DaprClient`, `TimeProvider` (`TryAdd`) and the sender as singletons; reference the library from ServiceDefaults, call it from `AddServiceDefaults()` in place of `AddDaprMessaging()` and delete `DaprMessagingExtensions.cs` (design D4); verify with `IntegrationEventsRegistrationTests` that `AddServiceDefaults()` resolves `IIntegrationEventSender` and a single singleton `DaprClient`
- [ ] 3.3 Remove `ServiceDefaults/DaprMessagingExtensionsTests.cs` from the Games test project (its coverage moved in 3.2); verify `dotnet test src/Games/HexMaster.Battleship.Games.Tests` passes

## 4. Replace the smoke round trip

- [ ] 4.1 Delete `SmokeEndpoints.cs` and its `MapSmokeEndpoints()`/`SmokeEventStore` wiring from the Games and Players APIs, and delete `Integration/SmokeMessagingTests.cs`; verify the solution builds and `grep -rni smoke src --include=*.cs` returns nothing
- [ ] 4.2 Add `Integration/IntegrationEventsDeliveryTests` (`[Trait("Category", "Integration")]`) that starts the AppHost, waits for the Events sidecar to list the `integration-events` subscription, opens `GET /events?playerId=<A>`, sends a `PlayerCreatedEvent` for A through `DaprIntegrationEventSender` with a `DaprClient` pointed at the Players sidecar, and asserts the stream delivers `player.created.v1` with the payload within five seconds (design D6); verify it passes locally with Docker and Dapr running

## 5. Frontend event names

- [ ] 5.1 Replace the placeholder entries in `src/Frontend/src/app/events/realtime-event-types.ts` with the seven catalog type names, update its comment to name `HexMaster.Battleship.IntegrationEvents` as the source, and switch sample types in `events.service` specs to catalog names (design D7); verify `npm test` and `npm run build` in `src/Frontend` pass

## 6. Integration check

- [ ] 6.1 Run `dotnet build src/Battleship.slnx` and `dotnet test src/Battleship.slnx` (including the Integration category) and start the AppHost once to confirm the `pubsub` component loads for all three sidecars on the dashboard; verify all pass and the dashboard shows no failing resource
