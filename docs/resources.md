# Resources

In Aspire, a **resource** is one thing the AppHost models and runs for you: a container, a .NET project, a JavaScript app, a parameter, a Dapr component, and so on. You declare each resource in C# in the AppHost, give it a name, and Aspire starts it, wires it to the resources it needs, and shows it on the dashboard with its state, endpoints, environment, console logs and health.

Battleship declares every resource in a single file, [`AppHost.cs`](../src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/AppHost.cs). Each one starts with a call on the builder:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var messaging = builder.AddRabbitMQ("messaging", rabbitUsername, rabbitPassword)
    .WithManagementPlugin();
```

The string, here `"messaging"`, is the resource name. It is the name you see on the dashboard, and the name other resources use to find this one.

![The Battleship AppHost resources grouped by kind: four parameters; the messaging and storage containers with the tables resource; the pubsub and game-cleanup Dapr components; the Games, Users and Events API projects, each with a Dapr sidecar; the YARP gateway; the JavaScript frontend; and the devbot custom resource with its own sidecar.](assets/help-resource-graph.svg)

## The resources of this app

The diagram above groups the resources by kind. The same information, with what each resource does:

| Kind | Resource | Added with | Role in this app |
|---|---|---|---|
| Parameter | `rabbitmq-username` | `AddParameter` | The RabbitMQ user. Defaults to `guest`. |
| Parameter | `rabbitmq-password` | `AddParameter` (secret, generated) | The RabbitMQ password. Generated on the first run and kept in the AppHost's user secrets. |
| Parameter | `users-signing-key` | `AddParameter` (secret, generated) | The key the Users API signs access tokens with, and every API validates them with. Generated and kept like the password. |
| Parameter | `game-cleanup-schedule` | `AddParameter` | When the cleanup of inactive games runs. Defaults to `@daily` in `appsettings.json`. |
| Container | `messaging` | `AddRabbitMQ(...).WithManagementPlugin()` | The RabbitMQ message broker, with its management UI. |
| Container | `storage` | `AddAzureStorage(...).RunAsEmulator(...)` | The Azurite storage emulator, with a persistent lifetime, so users and games survive an AppHost restart. |
| Child resource | `tables` | `storage.AddTables("tables")` | Table storage inside `storage`, used by the Users and Games APIs. |
| Dapr component | `pubsub` | `AddDaprComponent(..., "pubsub.rabbitmq")` | The pub/sub component on RabbitMQ that carries integration events. See [Dapr](dapr.md). |
| Dapr component | `game-cleanup` | `AddDaprComponent(..., "bindings.cron")` | The cron input binding that triggers the game cleanup. See [Dapr](dapr.md). |
| Project | `hexmaster-battleship-games-api` | `AddProject<...>().WithDaprSidecar(...)` | The Games API: creating, joining and playing games. Dapr app id `games-api`. |
| Project | `hexmaster-battleship-users-api` | `AddProject<...>().WithDaprSidecar(...)` | The Users API: anonymous users and their tokens. Dapr app id `users-api`. |
| Project | `hexmaster-battleship-events-api` | `AddProject<...>().WithDaprSidecar(...)` | The Events API: pushes events to browsers over server-sent events. Dapr app id `events-api`. |
| YARP gateway | `gateway` | `AddYarp("gateway")` | The single backend endpoint for the browser. It routes `/users` and `/games` to their APIs. |
| JavaScript app | `frontend` | `AddJavaScriptApp("frontend", ..., "start:aspire")` | The Angular app, served by `ng serve` on a port Aspire picks. |
| Custom resource | `devbot` | `AddBattleshipBot("devbot", ...)` | A bot opponent for local runs, with its own Dapr sidecar (app id `dev-bot`). See [Custom integration](custom-integration.md). |

Each Dapr sidecar also shows on the dashboard as a resource of its own, named after its app with `-dapr-cli` at the end. Its console logs show the sidecar loading its components.

## Parameters

A parameter is a value the AppHost passes to other resources, such as a user name, a password or a schedule. Aspire shows parameters on the dashboard and asks for a value when one is missing. The RabbitMQ password is generated the first time and persisted, so it stays the same on every run:

```csharp
var rabbitPassword = builder.AddParameter(
    "rabbitmq-password",
    new GenerateParameterDefault { MinLength = 22, Special = false },
    secret: true,
    persist: true);
```

`secret: true` hides the value on the dashboard. `persist: true` stores it in the AppHost's user secrets under `Parameters:rabbitmq-password`. Set that secret yourself if you want to choose the value.

## Containers

`AddRabbitMQ` and `AddAzureStorage(...).RunAsEmulator(...)` are hosting integrations: packages that know how to run a RabbitMQ or Azurite container, which ports it opens, and how other resources connect to it. You do not write an image name, a port mapping or a connection string. The storage emulator runs with `ContainerLifetime.Persistent`, so it keeps running, and keeps its data, after the AppHost stops.

## Projects

`AddProject<Projects.HexMaster_Battleship_Games_Api>(...)` runs a .NET project from the solution. Aspire builds it, gives it endpoints, and passes it environment variables such as connection strings and the OpenTelemetry settings that send its logs, traces and metrics to the dashboard. Each API also gets an **API reference** link on the dashboard, which opens its Scalar page.

Every API runs with a Dapr sidecar, added with `WithDaprSidecar`. What that means is described in [Dapr](dapr.md).

## The gateway and the frontend

`AddYarp("gateway")` runs a YARP reverse proxy. Its routes are built in code from the route table in `GatewayRoutes.cs`, so the browser talks to a single backend origin.

`AddJavaScriptApp("frontend", "../../../Frontend", "start:aspire")` runs the npm script `start:aspire` in the frontend folder. The AppHost passes the gateway URL in `BACKEND_GATEWAY_URL` and the port in `PORT`.

How these resources depend on each other, and in which order they start, is described in [Resource relations](resource-relations.md).
