# Spec Delta

## Purpose

Defines the bot opponent that runs with the local development environment, so one developer can play a full game of Battleship alone. Covers where it runs, how it finds and joins games, how it places its fleet and takes turns, its four intelligence presets, and how it is controlled from the Aspire dashboard.

## ADDED Requirements

### Requirement: The bot runs only in the local development environment
When the Aspire AppHost runs locally, it SHALL start a bot resource named `devbot` with a bot icon. The bot SHALL NOT be part of a published or deployed application.

#### Scenario: Local run
- **WHEN** the AppHost is started locally
- **THEN** the dashboard lists a `devbot` resource that reaches the running state once the gateway is available

#### Scenario: Publish
- **WHEN** the AppHost is run in publish mode
- **THEN** the produced manifest or deployment artifacts contain no bot resource

### Requirement: The bot plays as its own user through the public API
The bot SHALL obtain an anonymous user with the display name `Dev Bot` from the Users service, and SHALL call the games API only through the gateway with that user's access token. It SHALL refresh its tokens before the access token expires, and SHALL use the same user for every game it plays.

#### Scenario: Identity at start
- **WHEN** the bot starts
- **THEN** a user named `Dev Bot` exists, and every game the bot joins shows `Dev Bot` as player two

#### Scenario: Long session
- **WHEN** the bot has been running for longer than the access token lifetime
- **THEN** its next game request still succeeds with a refreshed token

### Requirement: The bot joins a new game after a grace period
With auto-join on, when a game is created the bot SHALL wait a grace period (default 10 seconds) and then join it. If another player took the seat first, the bot SHALL leave the game alone without logging an error. It SHALL never join a game it created itself.

#### Scenario: Nobody joins in time
- **WHEN** player A creates game `K7Q2M9XA` and no other player joins within 10 seconds
- **THEN** the bot joins `K7Q2M9XA` as player two

#### Scenario: A human joins first
- **WHEN** player A creates game `K7Q2M9XA` and player B joins it after 3 seconds
- **THEN** the bot does not become a player of `K7Q2M9XA`, and the game still has players A and B

### Requirement: Auto-join can be switched off and on
The bot SHALL start with auto-join on. While auto-join is off, the bot SHALL NOT join newly created games, and SHALL keep playing the games it already plays in.

#### Scenario: Auto-join off
- **WHEN** auto-join is switched off and player A creates a game that nobody joins for a minute
- **THEN** the bot does not join that game

#### Scenario: Games in progress continue
- **WHEN** auto-join is switched off while the bot plays game `K7Q2M9XA`
- **THEN** the bot keeps taking its turns in `K7Q2M9XA` until it ends

### Requirement: The bot can be sent to a specific game
On request with a game code, the bot SHALL join that game regardless of auto-join and the grace period, and play it. When the bot already plays in the game, it SHALL resume from the current game state. When the game is unknown, full or already started without the bot, the request SHALL fail with a message saying so.

#### Scenario: Join by code
- **WHEN** auto-join is off and the bot is sent to free game `K7Q2M9XA`
- **THEN** the bot joins `K7Q2M9XA` immediately and places its fleet

#### Scenario: Resume after a restart
- **WHEN** the AppHost restarted while the bot was playing started game `K7Q2M9XA`, and the bot is sent to `K7Q2M9XA`
- **THEN** the bot reads the game state and fires when it has the turn

#### Scenario: Full game
- **WHEN** the bot is sent to game `K7Q2M9XA`, which already has two other players
- **THEN** the request fails with a message that the game is full, and the game is unchanged

### Requirement: The bot places a random valid fleet and gets ready
After joining a game, the bot SHALL place all five ships at random positions that lie on the board and do not overlap, and then declare itself ready. Different games SHALL get different fleets.

#### Scenario: Ready after joining
- **WHEN** the bot joins game `K7Q2M9XA`
- **THEN** within a few seconds the bot has five placed ships in `K7Q2M9XA` and is ready, and the game is not yet started while the human is not ready

#### Scenario: Game starts when the human is ready
- **WHEN** the bot is ready in game `K7Q2M9XA` and the human player gets ready
- **THEN** the game is `Started`

### Requirement: The bot takes its turns until the game ends
In a started game, whenever the bot has the turn it SHALL fire one shot at a cell it has not shot at before, after a short delay (default 1 second). This holds whether the bot gets the first turn or receives the turn later. The bot SHALL stop playing a game once that game has ended.

#### Scenario: Bot has the first turn
- **WHEN** game `K7Q2M9XA` starts and the bot gets the first turn
- **THEN** the bot fires one shot about 1 second later and the turn passes to the human

#### Scenario: Turn passes to the bot
- **WHEN** the human fires in game `K7Q2M9XA` and the turn passes to the bot
- **THEN** the bot fires exactly one shot at a cell it has not shot at before

#### Scenario: Game ends
- **WHEN** either player sinks the other's last ship in game `K7Q2M9XA`
- **THEN** the bot fires no further shots in `K7Q2M9XA`

### Requirement: The bot plays at one of four intelligence presets
The bot SHALL choose its shots with the active preset:
- `Random`: any cell not shot yet;
- `HuntAndTarget`: random until a hit, then cells next to unresolved hits;
- `HuntWithParity`: like `HuntAndTarget`, but hunting only on checkerboard cells;
- `ProbabilityDensity`: the cell where the remaining ships fit in the most ways, given all hits and misses.

The bot SHALL start with the preset configured in the AppHost, default `HuntAndTarget`.

#### Scenario: Default preset
- **WHEN** the AppHost starts without a configured preset
- **THEN** the bot shows `HuntAndTarget` as its intelligence

#### Scenario: Hunt and target follows up a hit
- **WHEN** the preset is `HuntAndTarget` and the bot's last shot hit a cell whose neighbours it has not shot
- **THEN** the bot's next shot is at one of those neighbouring cells

#### Scenario: Parity while hunting
- **WHEN** the preset is `HuntWithParity` and the bot has no unresolved hit
- **THEN** the bot's next shot is at a cell whose column plus row is even

#### Scenario: Probability density picks the most likely cell
- **WHEN** the preset is `ProbabilityDensity`
- **THEN** the bot's next shot is at a cell with the highest count of remaining ship placements that are consistent with all its hits and misses

#### Scenario: Random never repeats
- **WHEN** the preset is `Random` and the bot has fired 143 shots in a game
- **THEN** its next shot is at the one cell it has not shot at

### Requirement: The intelligence preset can be changed while the bot runs
On request, the bot SHALL switch to the chosen preset and use it from its next shot in every game it plays, without a restart and without losing any game. A preset that is not one of the four SHALL be refused.

#### Scenario: Change mid-game
- **WHEN** the bot plays game `K7Q2M9XA` with `Random` and the preset is changed to `ProbabilityDensity`
- **THEN** the bot's next shot in `K7Q2M9XA` is chosen by `ProbabilityDensity` and the game continues

#### Scenario: Unknown preset
- **WHEN** a preset change to `Genius` is requested
- **THEN** the request fails with a message listing the four presets and the active preset is unchanged

### Requirement: The bot is controlled from the Aspire dashboard
The `devbot` resource SHALL offer these commands, enabled only while the bot is healthy:
- *Set intelligence*, with a required choice of the four presets;
- *Enable auto-join* and *Disable auto-join*, each enabled only when it changes the current setting;
- *Join game*, with a required game code.

Each command SHALL be usable from the dashboard and the Aspire CLI, and SHALL report success or the failure message.

#### Scenario: Set intelligence from the dashboard
- **WHEN** a developer runs *Set intelligence* on `devbot` and chooses `HuntWithParity`
- **THEN** the command reports success and the bot plays with `HuntWithParity` from its next shot

#### Scenario: Only the useful auto-join command is enabled
- **WHEN** auto-join is on
- **THEN** *Disable auto-join* is enabled and *Enable auto-join* is disabled

#### Scenario: Bot not healthy
- **WHEN** the `devbot` resource is starting or has stopped
- **THEN** none of its commands can be run

### Requirement: The bot shows its state on the dashboard
The `devbot` resource SHALL show as properties its active intelligence preset, whether auto-join is on, and the codes of the games it currently plays. It SHALL update them within a few seconds of any change.

#### Scenario: State after a change
- **WHEN** the preset is set to `Random` and the bot joins game `K7Q2M9XA`
- **THEN** the `devbot` resource properties show intelligence `Random`, auto-join on and game `K7Q2M9XA`

#### Scenario: Finished game disappears
- **WHEN** game `K7Q2M9XA` ends
- **THEN** `K7Q2M9XA` is no longer listed among the bot's games
