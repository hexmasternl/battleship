# Custom resource commands

Every resource on the Aspire dashboard has built-in commands, such as **Stop** and **Restart**. A **custom resource command** is one you add yourself in the AppHost: a named action, with an optional input dialog, that runs your own code when someone clicks it on the dashboard or runs it from the Aspire CLI. Your code decides what the command does and whether it succeeded, and the dashboard shows the result.

Battleship gives the `devbot` resource four commands to control the bot while it runs. They live in [`BotCommands.cs`](../src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/DevBot/BotCommands.cs).

## The dev bot commands

| Command | CLI | What it does to the bot |
|---|---|---|
| **Set intelligence** | `aspire resource devbot set-intelligence --intelligence Random` | Asks for a preset from a choice list (`Random`, `HuntAndTarget`, `HuntWithParity` or `ProbabilityDensity`) and switches the bot to it. It applies from the bot's next shot in every game. |
| **Enable auto-join** | `aspire resource devbot enable-auto-join` | The bot joins new games again when nobody else joins within the grace period. |
| **Disable auto-join** | `aspire resource devbot disable-auto-join` | The bot stops joining new games. Games in progress continue. |
| **Join game** | `aspire resource devbot join-game --code K7Q2M9XA` | Asks for an 8-character game code and sends the bot into that game now, whatever auto-join says. It fails with a message for an unknown, full or already started game. After an AppHost restart it also lets the bot resume a game it was playing. |

## How a command is declared

A command is added with `WithCommand` on the resource builder: a name, a display name, the code to run, and options.

```csharp
builder.WithCommand(JoinGame, "Join game",
    context => clientFor(bot).JoinGameAsync(
        (context.Arguments.GetString(CodeArgument) ?? string.Empty).Trim().ToUpperInvariant(),
        context.CancellationToken),
    new CommandOptions
    {
        Description = "Send the bot to a game by its code, or resume a game it plays after a restart.",
        IconName = "Play",
        Arguments = [CodeInput()],
        ValidateArguments = ValidateCode,
        UpdateState = WhenHealthy,
    });
```

- `Arguments` describes the input the dashboard asks for. *Set intelligence* asks for a choice of preset; *Join game* asks for a text field of at most 8 characters.
- `ValidateArguments` checks the input before the command runs. A game code must be exactly 8 letters and digits, such as `K7Q2M9XA`; anything else shows an error in the dialog.
- `UpdateState` decides whether the command is enabled.

## Enabled only while the bot is healthy

Every command is enabled only while the `devbot` resource is **healthy**, which its health check on `/health` reports. While the bot is starting, stopping or failing, the commands are greyed out. The auto-join pair is narrower still: *Enable auto-join* is only offered while auto-join is off, and *Disable auto-join* only while it is on.

## From the button to the bot and back

![A resource command end to end: 1, a command runs from the devbot resource on the dashboard and BotCommands checks it; 2, BotAdminClient in the AppHost calls the bot's admin API on its own endpoint; 3, BotStateMirror polls GET /admin/state every two seconds; 4, it publishes the bot's state as the devbot properties on the dashboard.](assets/help-resource-command-flow.svg)

The command code runs inside the AppHost, not inside the bot. The round trip, step by step:

1. **Run the command.** You click a command on the `devbot` resource, or run it with `aspire resource devbot ...`. `BotCommands` checks the input and that the bot is healthy.
2. **Call the bot.** `BotAdminClient` calls the bot's internal admin API on the bot's own HTTP endpoint, never through the gateway: `PUT /admin/intelligence`, `PUT /admin/auto-join` or `POST /admin/games/{code}`. A success answer becomes a success message on the dashboard; a problem answer becomes a failure with its detail.
3. **Read the state.** Once the bot is ready, `BotStateMirror` polls `GET /admin/state` every two seconds.
4. **Show the state.** When the state changed, it publishes it as properties of the `devbot` resource.

## Live properties

The dashboard shows the bot's state as three properties on the `devbot` resource, updated within a few seconds of a change:

| Property | Shows |
|---|---|
| `devbot.intelligence` | The current preset. |
| `devbot.auto-join` | Whether the bot joins new games by itself. |
| `devbot.games` | The codes of the games the bot is playing, or `none`. |

So after you run *Set intelligence* with `Random`, `devbot.intelligence` changes to `Random` a moment later: proof that the command reached the bot. How the bot itself is added to the AppHost is described in [Custom integration](custom-integration.md).
