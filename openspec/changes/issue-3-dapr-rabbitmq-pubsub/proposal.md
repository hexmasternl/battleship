# Proposal

> Tracks GitHub issue [hexmasternl/battleship#3](https://github.com/hexmasternl/battleship/issues/3).

## Why

The Games and Players services cannot exchange anything asynchronously: the Aspire AppHost in `src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/AppHost.cs` registers the two API projects and nothing else, so there is no message broker, no Dapr sidecar and no pub/sub component anywhere in the solution. The game domain will raise integration events (game created, shot fired, game finished) that other services and the real-time Events module (issue #4) must consume, so the messaging backbone has to exist before any of that work can start.

## What Changes

- Add a RabbitMQ container resource named `messaging` to the AppHost with the management plugin enabled, so a local run brings up the broker and its management UI alongside the services.
- Model Dapr in the AppHost: a Dapr pub/sub component named `pubsub` whose broker is the `messaging` RabbitMQ resource (the connection details flow from the resource, not from a hand-written connection string), and a Dapr sidecar for the Games API and for the Players API that loads that component.
- Give both APIs a Dapr client and the subscription plumbing (Dapr client registration through ServiceDefaults, subscribe handler and cloud-events middleware in each API pipeline) so that any service can publish to and subscribe from `pubsub` through the Dapr SDK.
- Add a throwaway end-to-end smoke test in the Games test project: an event published by one API arrives at a subscriber in the other API through RabbitMQ.
- Document the local prerequisites (Docker, the Dapr CLI and `dapr init`) in the README and make the AppHost fail fast with a clear message when Dapr is not initialised.

## Capabilities

### New Capabilities
- `local-messaging`: the asynchronous messaging backbone of the local development environment: the broker the AppHost runs, the Dapr pub/sub component the services share, how each service publishes and subscribes through its sidecar, and what a developer must have installed for the AppHost to start.

### Modified Capabilities
<!-- none: there are no existing specs -->

## Impact

- `src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/AppHost.cs` and its `.csproj`: new hosting packages (`Aspire.Hosting.RabbitMQ` and the Community Toolkit Dapr hosting integration), the RabbitMQ resource, the Dapr component and the sidecars.
- `src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.ServiceDefaults`: a shared extension that registers the Dapr client, so every API gets it from `AddServiceDefaults()`.
- `src/Games/HexMaster.Battleship.Games.Api/Program.cs` and `src/Players/HexMaster.Battleship.Players.Api/Program.cs`: subscribe handler, cloud-events middleware and a smoke-test endpoint pair; the `Dapr.AspNetCore` package.
- `src/Games/HexMaster.Battleship.Games.Tests`: the end-to-end smoke test (Aspire testing host).
- `README.md`: a "Running locally" section with the prerequisites.
- New developer prerequisites: Docker (already needed by Aspire for containers), the Dapr CLI and `dapr init`. Nothing changes for production deployment; that is a later change.
- No frontend change. No integration event contracts of the game domain are defined here.
