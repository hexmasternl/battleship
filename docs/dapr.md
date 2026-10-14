# Dapr

[Dapr](https://dapr.io) gives an app building blocks, such as publishing messages or running code on a schedule, through a small helper process next to it. Battleship uses two of those building blocks: **pub/sub**, to send integration events between services, and an **input binding**, to run the daily game cleanup. This page explains how both are wired in the AppHost.

## What a sidecar is

A Dapr **sidecar** is a separate process, `daprd`, that runs next to your app and only serves that app. The app talks to its sidecar over HTTP or gRPC on the local machine. The sidecar talks to the outside world, such as a message broker, through **components**. The app never needs a RabbitMQ client library or the broker's address: it only knows the component's name.

In this app, four resources run a sidecar. Each sidecar has a Dapr **app id**:

| Resource | Dapr app id | What its sidecar does |
|---|---|---|
| `hexmaster-battleship-games-api` | `games-api` | Publishes integration events, and receives the cron trigger for the game cleanup. |
| `hexmaster-battleship-users-api` | `users-api` | Loads the pub/sub component. The Users API publishes no events today. |
| `hexmaster-battleship-events-api` | `events-api` | Subscribes to the integration events and hands them to the Events API. |
| `devbot` | `dev-bot` | Subscribes to the integration events, so the bot learns about new games and turns. |

![Dapr in Battleship: games-api, users-api, events-api and devbot each run a daprd sidecar. games-api publishes to the pubsub component, which runs on the messaging RabbitMQ container and carries the topic integration-events; events-api and devbot subscribe to it; users-api loads it but publishes nothing. The game-cleanup cron binding posts to /maintenance/game-cleanup on games-api.](assets/help-dapr-sidecars.svg)

## Sidecars in the AppHost

The [Community Toolkit Dapr integration](https://github.com/CommunityToolkit/Aspire) adds three things to the AppHost: `AddDapr()`, `AddDaprComponent(...)` and `WithDaprSidecar(...)`. A sidecar is attached to a project like this:

```csharp
var gamesApi = builder.AddProject<Projects.HexMaster_Battleship_Games_Api>(GatewayRoutes.GamesApi)
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "games-api", Config = daprConfig })
        .WithReference(pubsub)
        .WithReference(gameCleanup));
```

The sidecar references the components it loads. Each sidecar appears on the dashboard as a resource of its own, named after its app with `-dapr-cli` at the end, and its console logs show the components loading and the subscriptions it found.

`Config = daprConfig` points every sidecar at a Dapr configuration that the AppHost writes for each run. It samples every request and sends the sidecar's spans to the Aspire dashboard, so one trace runs from the publishing API through the sidecars to the Events API.

## The pub/sub component on RabbitMQ

The component is named `pubsub` and has the type `pubsub.rabbitmq`. Its settings are resolved from the `messaging` container and the RabbitMQ parameters when the AppHost starts, so no address or password is written to a file in the repository:

```csharp
var pubsub = builder.AddDaprComponent("pubsub", "pubsub.rabbitmq")
    .WithMetadata("hostname", messagingHostAndPort)
    .WithMetadata("username", rabbitUsername.Resource)
    .WithMetadata("password", rabbitPassword.Resource)
    .WithMetadata("durable", "true")
    .WithMetadata("deletedWhenUnused", "false")
    .WithMetadata("requeueInFailure", "true");
```

Messages are durable, and when a handler fails the message is put back on the queue so the sidecar delivers it again.

Every event travels on one topic, `integration-events`:

- **Publishing.** The Games API publishes through `DaprIntegrationEventSender`, which calls `PublishEventAsync("pubsub", "integration-events", envelope)` on its sidecar. The envelope says who should receive the event.
- **Subscribing.** The Events API and the dev bot map an endpoint with `.WithTopic("pubsub", "integration-events")`. Their sidecar finds that subscription at start-up and posts every message on the topic to it.
- **One queue per subscriber.** The RabbitMQ component creates a queue for each app id, so the Events API and the dev bot each receive every event.

## The cron binding for the game cleanup

Games without activity for longer than the retention period (90 days by default) are removed once a day. No timer runs inside the Games API for this. A Dapr **cron input binding** triggers it instead:

```csharp
var gameCleanupSchedule = builder.AddParameter("game-cleanup-schedule");
var gameCleanup = builder.AddDaprComponent("game-cleanup", "bindings.cron")
    .WithMetadata("schedule", gameCleanupSchedule.Resource)
    .WithMetadata("route", "/maintenance/game-cleanup")
    .WithMetadata("direction", "input");
```

On every tick of the schedule, the Games API's sidecar posts to `/maintenance/game-cleanup` on the Games API. Only that sidecar references `game-cleanup`, so no other service is triggered. The gateway never routes `/maintenance`, so the cleanup cannot be called from a browser.

The schedule is the AppHost parameter `game-cleanup-schedule`, `@daily` by default in `appsettings.json`. To watch the cleanup run locally, set a shorter schedule in the AppHost's user secrets and restart the AppHost:

```shell
dotnet user-secrets set "Parameters:game-cleanup-schedule" "@every 1m" \
  --project src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost
```

## Dapr must be installed

Sidecars need the Dapr CLI and the runtime that `dapr init` installs. The AppHost checks for both before it starts any resource and stops with a clear message when one is missing. See [Prerequisites](prerequisites.md).
