# Custom integration

Aspire ships hosting integrations for many technologies: `AddRabbitMQ`, `AddAzureStorage`, `AddYarp` and the rest. You can write your own the same way. A **custom hosting integration** is two things:

1. a **custom resource type**, a class that describes what the resource is, and
2. **builder extension methods**, such as `AddSomething(...)` and `WithSomething(...)`, that add the resource to the AppHost and configure it.

Battleship has one: the **dev bot**, a bot opponent for local runs, so one developer can play a whole game alone. Its code is in the AppHost's [`DevBot`](../src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/DevBot/) folder, and the bot itself is the worker project [`HexMaster.Battleship.DevBot`](../src/DevTools/HexMaster.Battleship.DevBot/).

## One builder call

The whole bot is added in `AppHost.cs` with one call and three settings:

```csharp
if (builder.ExecutionContext.IsRunMode)
{
    builder.AddBattleshipBot("devbot", gateway, pubsub, daprConfig)
        .WithIntelligence("HuntAndTarget")
        .WithJoinGracePeriod(TimeSpan.FromSeconds(10))
        .WithShotDelay(TimeSpan.FromSeconds(1));
}
```

The `IsRunMode` check keeps the bot out of anything but a local run.

![The dev bot integration: AddBattleshipBot followed by WithIntelligence, WithJoinGracePeriod and WithShotDelay adds the devbot resource, a BattleshipBotResource that runs HexMaster.Battleship.DevBot. The call gives it a Dapr sidecar with app id dev-bot, a gateway reference with WaitFor, a health check on /health, DevBot environment variables for its settings, and four dashboard commands with three live properties.](assets/help-devbot-integration.svg)

## The custom resource type

`BattleshipBotResource` derives from `ProjectResource`, so Aspire runs it like any other .NET project. The class adds what is specific to the bot: its Dapr app id, the names of its dashboard properties and environment variables, and its HTTP endpoint, where the AppHost reaches the bot's admin API.

```csharp
public sealed class BattleshipBotResource(string name) : ProjectResource(name)
{
    public const string DaprAppId = "dev-bot";
    // ...
    public EndpointReference HttpEndpoint => field ??= new EndpointReference(this, "http");
}
```

## What `AddBattleshipBot` wires up

[`BattleshipBotResourceBuilderExtensions.cs`](../src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/DevBot/BattleshipBotResourceBuilderExtensions.cs) adds the resource, points it at the `HexMaster.Battleship.DevBot` project, and then chains everything the bot needs. The diagram shows the same list:

- **Its own Dapr sidecar.** `WithDaprSidecar` with app id `dev-bot`, loading the shared `pubsub` component. Because the bot has its own app id, it gets its own queue on the `integration-events` topic and receives every event. See [Dapr](dapr.md).
- **A gateway reference.** `WithReference(gateway)` lets the bot call `https+http://gateway/` through service discovery, and `WaitFor(gateway)` keeps it from starting before the gateway runs. The bot plays through the public API, exactly like the browser.
- **A health check.** `WithHttpHealthCheck("/health")` makes the dashboard show whether the bot is healthy. The bot's commands are only enabled while it is.
- **Dashboard commands and live properties.** `WithBotCommands()` and `WithBotStateProperties()` add the commands and the properties described in [Custom resource commands](resource-commands.md).
- **Dashboard details.** A bot icon (`WithIconName("Bot")`), and `ExcludeFromManifest()`, so the bot never ends up in a deployment.

## Configuration

Each `With...` method validates its value and passes it to the worker as a `DevBot__*` environment variable:

| Method | Environment variable | Default | Meaning |
|---|---|---|---|
| `WithIntelligence(...)` | `DevBot__Intelligence` | `HuntAndTarget` | The playing strength. An unknown preset stops the AppHost with the list of valid presets. |
| `WithJoinGracePeriod(...)` | `DevBot__JoinGracePeriod` | 10 seconds | How long the bot leaves a new game to a human before it joins. |
| `WithShotDelay(...)` | `DevBot__ShotDelay` | 1 second | How long the bot waits before each shot, so you can follow its moves. |

The intelligence presets, weakest first, are deterministic shot strategies:

| Preset | How it chooses a shot |
|---|---|
| `Random` | Any cell it has not shot at. |
| `HuntAndTarget` | Random until it hits, then the cells next to its hits. |
| `HuntWithParity` | Like `HuntAndTarget`, but it hunts only on a checkerboard pattern of cells. |
| `ProbabilityDensity` | The cell covered by the most ways the fleet's ships could lie around its misses, favouring placements through its hits. |

## How the bot plays

The bot is a local opponent that behaves like a second player:

1. When it starts, it creates its own user, **Dev Bot**, through the gateway.
2. Its sidecar delivers every integration event. When another player creates a game, the bot waits for the grace period.
3. If nobody else took the second seat by then, and auto-join is on, it joins the game as player two, places a random fleet and gets ready.
4. Whenever it has the turn, it waits for the shot delay and fires one shot, chosen by its preset, until the game ends.

The grace period lets two people testing in two browsers still take the seat themselves. The bot plays any number of games at once. You can change its preset and auto-join while it runs, or send it into a specific game, with [custom resource commands](resource-commands.md).
