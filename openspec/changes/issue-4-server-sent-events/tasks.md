# Tasks

## 1. Events service scaffolding

- [x] 1.1 Create `src/Events/HexMaster.Battleship.Events.Abstractions`, `src/Events/HexMaster.Battleship.Events`, `src/Events/HexMaster.Battleship.Events.Api` (web SDK, referencing ServiceDefaults, with `AddServiceDefaults()` and `MapDefaultEndpoints()` and no weather endpoint) and `src/Events/HexMaster.Battleship.Events.Tests` (xUnit stack matching the Games tests) on `net10.0`, add them to `src/Battleship.slnx` under an `/Events/` folder, and verify `dotnet build src/Battleship.slnx` succeeds
- [x] 1.2 Add `IntegrationEventEnvelope` and `EventTarget` to the Abstractions project per design.md, with a validator that rejects a missing id, type or target kind and an empty target list for `player` and `game`, and verify xUnit tests cover an accepted envelope and each rejection

## 2. Routing core

- [x] 2.1 Implement `ConnectionRegistry` (register, unregister, connections for a player, all connections) and `AddEventsCore()` in the Events project, and verify xUnit tests cover one player with two connections and removal
- [x] 2.2 Implement `EventRouter` with the per-process sequence counter and the `player`, `game` and `all` rules, removing a connection whose channel is completed and continuing with the others, and verify xUnit tests cover the four routing scenarios of the `realtime-events` spec and the one-broken-connection scenario
- [x] 2.3 Implement `ReplayBuffer` (capacity 1000, max age 2 minutes, both from `Events:Replay` configuration) with replay filtered by player id and sequence greater than the presented id, falling through to live-only when the id is older than the buffer, and verify xUnit tests cover replay in order, no leak of another player's events, the fallen-out id and overflow

## 3. Stream endpoint and subscription

- [x] 3.1 Implement `GET /events` with `IConnectionIdentityResolver` and the `QueryStringIdentityResolver` (`playerId`), a bounded per-connection `Channel<SseItem<JsonElement>>` (capacity 256, drop-oldest with a warning), `TypedResults.ServerSentEvents`, disabled response buffering, `Last-Event-ID` replay before live events, and `400` problem details without a player id, and verify an in-process `WebApplicationFactory` test reads `text/event-stream` headers from an open stream and a `400` for a missing player id
- [ ] 3.2 Add the 15-second heartbeat as an `SseItem` with event type `heartbeat` and no id, driven by `TimeProvider` and reset by any real event, and remove the connection when the write fails, and verify an xUnit test with a fake `TimeProvider` observes a heartbeat after an idle period and none while events flow
- [ ] 3.3 Add `Dapr.AspNetCore`, `AddDaprClient()`, `UseCloudEvents()`, `MapSubscribeHandler()` and `POST /integration-events` subscribed to topic `integration-events` on component `pubsub` (both names from configuration with those defaults) that validates the envelope, routes it, returns `200` for delivered and unmatched events and `400` problem details for invalid ones, and verify the integration test opens `/events?playerId=A`, posts a `player` envelope for A and parses an SSE message with the sequence id, the envelope type and the payload within one second, and that an envelope for B is not received by A
- [ ] 3.4 Add the CORS policy from `Events:AllowedOrigins` (default `http://localhost:4200`) scoped to `GET /events`, and verify an integration test request with that `Origin` header receives the matching `Access-Control-Allow-Origin` header while another origin does not

## 4. Aspire hosting

- [ ] 4.1 Register the Events API in `AppHost.cs` with the Dapr sidecar and `pubsub` component reference following the pattern merged from `issue-3-dapr-rabbitmq-pubsub`, run the AppHost, and verify the dashboard shows `hexmaster-battleship-events-api` healthy with its sidecar
- [ ] 4.2 Publish an envelope with `dapr publish --publish-app-id hexmaster-battleship-events-api --pubsub pubsub --topic integration-events --data '<envelope>'` while a browser or `curl -N` holds `/events?playerId=A` open, verify the event arrives, and record the command, the single-instance limitation and the `Events:*` configuration keys in `README.md`

## 5. Frontend events service

- [ ] 5.1 Create `src/Frontend/src/app/events/` with `realtime-event-types.ts`, the `RealtimeEvent` model, the `EVENTS_API_URL` and `EVENT_SOURCE_FACTORY` injection tokens (provided in `app.config.ts` with the development Events API URL and the native `EventSource`), and `EventsService` with idempotent `connect(playerId)`, `disconnect()`, `events$` and the `state` signal per design.md, closing the stream through the root `DestroyRef`, and verify `ng build` succeeds
- [ ] 5.2 Write Vitest tests with a fake `EventSource` covering: one stream per connect, repeated connect is idempotent, a named event is surfaced with type, id and parsed payload, `heartbeat` is not surfaced, malformed JSON is skipped and logged, the state sequence `connecting` to `open` to `reconnecting`, the reconnect request URL carrying the last event id when the fake reconnects, explicit disconnect closing the stream without reconnection, and teardown closing the stream, and verify `ng test` passes
- [ ] 5.3 Document in `src/Frontend/README.md` how to set `EVENTS_API_URL` to the running Events API and how to confirm events arrive in the browser network panel, and verify the steps work against the AppHost from task 4.1

## 6. Integration

- [ ] 6.1 Run `dotnet build src/Battleship.slnx`, `dotnet test src/Battleship.slnx`, `ng build` and `ng test` from a clean checkout, and verify all succeed with no warnings introduced by this change
