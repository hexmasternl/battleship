# Proposal

> Tracks GitHub issue [hexmasternl/battleship#4](https://github.com/hexmasternl/battleship/issues/4).

## Why

Nothing in the solution pushes data to a browser: the AppHost hosts only the Games API and the Players API, neither has a streaming endpoint, and the Angular application under `src/Frontend/` has no way to learn that an opponent has placed ships or fired a shot other than polling. Battleship is a two-player, turn-based game, so the client must be told about the other player's moves the moment they happen; adding a dedicated Events service now, on top of the Dapr `pubsub` component from `issue-3-dapr-rabbitmq-pubsub`, gives every later game feature one real-time channel to publish into instead of each service inventing its own.

## What Changes

- Add a new `src/Events` service group with the same four projects as Games and Players (`HexMaster.Battleship.Events`, `.Abstractions`, `.Api`, `.Tests`), add it to `src/Battleship.slnx` under an `/Events/` solution folder, and register the API in the Aspire AppHost with a Dapr sidecar that loads the shared `pubsub` component.
- Define the integration event envelope in `HexMaster.Battleship.Events.Abstractions`: an id, an event type, a target kind (`player`, `game` or `all`), the target player ids and a JSON payload. Publishers (the Games and Players services, later) reference this project so every event on the `integration-events` topic has the same shape.
- Subscribe the Events API to the `integration-events` topic on `pubsub` through the Dapr SDK (cloud-events middleware, subscribe handler and a topic endpoint), so an event published by any service with a sidecar reaches the Events API.
- Expose `GET /events` on the Events API as a long-lived `text/event-stream` response built on ASP.NET Core 10's native server-sent events support: each message carries an `id`, an `event` type and the JSON payload as `data`, a heartbeat keeps idle connections alive, and a client that reconnects with `Last-Event-ID` receives the events it missed from a bounded replay buffer.
- Route events instead of broadcasting them: a player-targeted event reaches only that player's connections, a game-targeted event reaches only the connections of the players listed in the event, and an untargeted event reaches everyone. The connecting player is identified by a `playerId` query parameter until `issue-7-user-identification` delivers access tokens.
- Add an injectable events service to the Angular application that opens the `/events` stream, exposes received events as an observable and the connection state as a signal, relies on the browser's automatic reconnection with `Last-Event-ID`, and closes the stream when the application is destroyed.
- Cover the routing rules, envelope serialisation, heartbeat and replay with xUnit tests, add an in-process integration test that posts an event to the topic endpoint and reads it back from `/events`, and cover the frontend service with Vitest tests against a fake `EventSource`.

## Capabilities

### New Capabilities
- `realtime-events`: how the server delivers integration events to connected browsers in real time: the event envelope, the server-sent events stream and its message format, targeting and routing rules, keep-alive, reconnection and replay, and the subscription to the shared pub/sub component.
- `realtime-events-client`: how the frontend consumes that stream: opening the connection for the current player, surfacing events and connection state to the application, reconnecting after a drop, and releasing the connection when the application shuts down.

### Modified Capabilities
<!-- none: there are no existing specs. The pub/sub infrastructure is defined by issue-3-dapr-rabbitmq-pubsub and consumed here, not changed -->

## Impact

- New directory `src/Events/` with four projects; `src/Battleship.slnx` gains an `/Events/` folder; `src/Aspire/.../AppHost.cs` gains the Events API project with a Dapr sidecar and the `pubsub` reference.
- New NuGet dependencies in the Events API only: `Dapr.AspNetCore` (subscription and `DaprClient`). Server-sent events need no package; `TypedResults.ServerSentEvents` ships with ASP.NET Core 10. Tests use the xUnit stack already used by the Games and Players test projects plus `Microsoft.AspNetCore.Mvc.Testing`.
- `src/Frontend/src/app/`: a new `events` feature folder with the service, its injection token for the Events API base URL and tests; `src/Frontend/README.md` documents how to point the service at a running Events API. No new npm dependencies.
- The Events API allows cross-origin requests from the Angular dev server origin for `/events`, since the browser connects directly to the API during development.
- Depends on `issue-3-dapr-rabbitmq-pubsub` being merged into `development` first: the `pubsub` Dapr component, the RabbitMQ resource and the sidecar registration pattern come from that change and are reused, not redefined.
- Relates to `issue-7-user-identification`: until it lands, `/events` identifies the player from a query parameter; the identity resolution is isolated so that switching to the access token is a single change. No authorization is enforced by this change.
- Single-instance only: routing state lives in the process memory of the Events API. Running more than one instance is out of scope and is called out as a risk in design.md.
- The Games and Players services are not changed; they gain the ability to publish events through the shared envelope in later changes.
