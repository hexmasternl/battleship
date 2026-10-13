# Workshop suggestions: working with Aspire

This repository is meant to be the demo project for a workshop on working with Aspire. It already
runs a realistic distributed app: Dapr sidecars, RabbitMQ, Azurite table storage, a YARP gateway and
an Angular frontend, all orchestrated by the AppHost in
`src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost`.

The AppHost also contains hand-rolled code (`DaprPrerequisites`, `DaprTracingConfiguration`,
`GatewayRoutes.Validate`) that maps directly onto Aspire extension points. That gives each workshop
module a real "before" to refactor, instead of a toy example.

This document lists the opportunities, grouped by extension point, followed by a suggested order for
the workshop.

## 1. Custom resource commands (`WithCommand`)

The lowest-friction first module: dashboard buttons that do something useful for this stack.

| Resource | Command | What it does |
| --- | --- | --- |
| `games-api` | Seed demo games | Creates a handful of games in known states, so demos do not depend on clicking through the UI. |
| `storage` / `tables` | Reset game data | Clears the Azurite tables. The emulator has a persistent lifetime, so data survives restarts and attendees will want a way to start clean. |
| `messaging` | Purge queues | Clears stuck messages through the RabbitMQ management API, for example after a requeue loop caused by a failing handler. |
| `users-api` | Issue test token | Creates a test user and returns a bearer token, ready to paste into the Scalar API reference. |
| `events-api` | Publish test event | Sends an integration event (such as `GameShotFiredEvent` or `GameEndedEvent`) through Dapr and shows it arriving over server-sent events in the browser. |

## 2. Interaction Service

Commands that ask for input, and notifications instead of exceptions.

- **Publish test event, with a form.** Prompt for the event type (from `IntegrationEventCatalog`),
  the game code and the audience before publishing.
- **Rotate signing key.** Ask for confirmation, regenerate the `users-signing-key` parameter and
  restart `users-api` and `games-api`. Combines parameters, the interaction service and resource
  restarts from a command in one flow.
- **Rework `DaprPrerequisites`.** Today it throws at start-up when the Dapr CLI or runtime is
  missing. Turn that into a dashboard notification with a link to the install docs and a "Retry"
  action. The before/after comparison is a strong teaching moment.

## 3. Custom hosting integrations

- **Extract the Dapr glue into a reusable integration.** Package `DaprPrerequisites` and
  `DaprTracingConfiguration` as an integration with an extension method such as
  `.WithDaprTracingToDashboard()`, with its own tests. Teaches how to author an integration and
  what makes one reusable.
- **A custom `BattleshipBotResource`.** For example:

  ```csharp
  builder.AddBattleshipBot("admiral")
      .WithReference(gateway)
      .WithStrategy(BotStrategy.Hunt);
  ```

  The bot has its own resource type and icon, and publishes live state to the dashboard through
  `ResourceNotificationService` ("Waiting for opponent", "Playing ABC123 – 12 hits"). With
  `WithExplicitStart`, attendees start a bot from the dashboard and play against it. This is the
  headline demo.

  > Depends on the open gameplay changes (`issue-17-placement-phase`, `issue-18-game-play-page`,
  > `issue-19-game-end`). The Games service currently only supports creating and joining a game.

## 4. Eventing and lifecycle hooks

- Move the procedural set-up in `AppHost.cs` (validating the gateway routes, writing the Dapr
  tracing configuration) into `BeforeStartEvent` subscribers.
- Use `OnResourceReady` on `tables` to create tables and seed data, instead of each service doing
  that at start-up.
- Add custom health checks with `WithHealthCheck`, for example "is the Dapr sidecar healthy" or
  "does every gateway prefix resolve to a running API". `WaitFor` then means more than "the process
  started".

## 5. Dashboard customization

- Use `WithUrls` to add a Scalar API reference link per API (ties in with
  `issue-25-scalar-api-reference`), a "Play" link on the frontend and a friendly "RabbitMQ admin"
  link on the broker.
- Group resources with `WithParentRelationship`, for example sidecars under the API they serve.

## 6. Testing the AppHost

- Use `Aspire.Hosting.Testing` (`DistributedApplicationTestingBuilder`) to start the real resource
  graph, execute a custom command and assert its effect. Most attendees do not know the AppHost
  itself can be tested this way.

## 7. Publish and deploy extensibility (advanced)

- Add a custom pipeline step for `aspire publish` / `aspire deploy` that generates the Dapr component
  YAML and gateway configuration for Kubernetes or Azure Container Apps from the same model. That
  makes "the AppHost is the single source of truth" hold for deployment as well as local runs.

## Suggested workshop arc

1. **Dashboard URLs and a first command** – "Reset game data".
2. **Commands with input** – "Publish test event" and "Rotate signing key" via the Interaction
   Service.
3. **Lifecycle events and health checks** – refactor the existing AppHost set-up code.
4. **Your own integration** – extract the Dapr helpers into a reusable package.
5. **A custom resource** – the Battleship bot (once the gameplay changes have landed).
6. **Testing the AppHost** – integration tests over the commands built earlier.
7. **Publish and deploy** – a custom pipeline step (optional, time permitting).

Each step builds on the previous one and touches code that already exists in this repository.
