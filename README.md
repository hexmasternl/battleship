<div align="center">

<img src="docs/assets/battleship-banner.png" alt="Battleship: command the fleet, hunt the enemy, sink them all" width="100%">

<h3>A real-time, two-player Battleship game, and a working tour of <a href="https://aspire.dev">Aspire</a>.</h3>

<p>
<a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-00c0ff?style=for-the-badge&labelColor=041e32&logo=dotnet&logoColor=e9eef3"></a>
<a href="https://aspire.dev"><img alt="Aspire 13.6" src="https://img.shields.io/badge/Aspire-13.6-00c0ff?style=for-the-badge&labelColor=041e32"></a>
<a href="https://dapr.io"><img alt="Dapr" src="https://img.shields.io/badge/Dapr-pub%2Fsub-2f87ff?style=for-the-badge&labelColor=041e32&logo=dapr&logoColor=e9eef3"></a>
<a href="https://www.rabbitmq.com"><img alt="RabbitMQ" src="https://img.shields.io/badge/RabbitMQ-broker-2f87ff?style=for-the-badge&labelColor=041e32&logo=rabbitmq&logoColor=e9eef3"></a>
<a href="https://angular.dev"><img alt="Angular 22" src="https://img.shields.io/badge/Angular-22-ff2146?style=for-the-badge&labelColor=041e32&logo=angular&logoColor=e9eef3"></a>
<a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/badge/license-MIT-7a8694?style=for-the-badge&labelColor=041e32"></a>
</p>

<p>
<a href="#-quick-start"><b>Quick start</b></a> &nbsp;·&nbsp;
<a href="#what-this-demo-shows-about-aspire"><b>Aspire tour</b></a> &nbsp;·&nbsp;
<a href="#architecture"><b>Architecture</b></a> &nbsp;·&nbsp;
<a href="#services"><b>Services</b></a> &nbsp;·&nbsp;
<a href="#how-we-work"><b>How we work</b></a> &nbsp;·&nbsp;
<a href="docs/design-system.md"><b>Design system</b></a>
</p>

</div>

---

## Briefing

Two admirals, two hidden fleets, one 12×12 grid each. Players join without registering, place
five ships and take turns firing until only one fleet is still afloat. Every shot reaches the
opponent's screen in real time.

The game is also a **reference project for building distributed .NET apps with Aspire**. One
`aspire run` starts the whole thing:

- an Angular frontend
- three ASP.NET Core APIs, each with a Dapr sidecar
- a YARP gateway
- a RabbitMQ broker
- an Azure Storage emulator
- the Aspire dashboard, which shows all of it

You don't need a cloud account, a `docker-compose.yml`, or any connection string written by hand.

> [!NOTE]
> The game is being built in the open. Player identity, the gateway and real-time events are
> done. Creating, joining, placing ships, battle and the end of the game are planned as OpenSpec
> changes (see [Roadmap](#roadmap)). The infrastructure is complete, so the project is already
> useful as an Aspire example.

---

## ⚓ Quick start

### 1. Install the prerequisites

| Prerequisite | Why | Check |
|---|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running) | RabbitMQ and Azurite run as containers, and `dapr init` runs its own | `docker version` |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Builds and runs the services | `dotnet --version` |
| [Aspire CLI](https://aspire.dev/get-started/install-cli/) | Runs the AppHost and opens the dashboard | `aspire --version` |
| [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) | Starts the sidecars | `dapr --version` |
| [Node.js](https://nodejs.org) with npm | Runs the Angular dev server | `node --version` |

### 2. One-time setup

```shell
dapr init                              # downloads daprd, starts the placement and scheduler containers
cd src/Frontend && npm install && cd -  # frontend packages
```

> [!TIP]
> `dapr init` also starts Redis and Zipkin containers. This solution doesn't use them, and they
> don't conflict with the containers the AppHost starts.

### 3. Launch

```shell
cd src/Aspire/HexMaster.Battleship.Aspire
aspire run
```

The CLI builds the solution, starts the AppHost, prints the dashboard URL and opens it in your
browser. When every resource is **Running**, open the `frontend` endpoint and play.

To stop, press `Ctrl+C`, or run `aspire stop` from the same folder.

> [!IMPORTANT]
> If the AppHost stops right away with a message about Dapr, either the Dapr CLI is missing or
> `dapr init` hasn't been run on this machine. The message names the missing step. That
> fail-fast check is [`DaprPrerequisites.cs`](src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/DaprPrerequisites.cs).

---

## What this demo shows about Aspire

Everything below is in a single file:
[`AppHost.cs`](src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/AppHost.cs).
Read it top to bottom. It is short and every block has a comment.

| Aspire concept | How Battleship uses it |
|---|---|
| **Projects as resources** | `AddProject<...>()` runs the Players, Games and Events APIs with service discovery, health checks and telemetry wired in. |
| **Containers as resources** | `AddRabbitMQ("messaging").WithManagementPlugin()` starts a broker with its management UI. |
| **Azure emulators** | `AddAzureStorage("storage").RunAsEmulator(...)` runs Azurite for table storage, so no Azure subscription is needed. |
| **Persistent containers** | `WithLifetime(ContainerLifetime.Persistent)` keeps Azurite, and your players, alive between runs. |
| **Generated secret parameters** | `AddParameter(..., new GenerateParameterDefault { ... }, secret: true, persist: true)` creates the RabbitMQ password and the JWT signing key on first run and stores them in user secrets. |
| **Reference expressions** | `ReferenceExpression.Create($"{host}:{port}")` builds the Dapr component's broker address from the live RabbitMQ endpoint, so it is never written to a file. |
| **Dapr sidecars** | The [Community Toolkit](https://github.com/CommunityToolkit/Aspire) adds `AddDapr()`, `AddDaprComponent(...)` and `WithDaprSidecar(...)`, so each API gets its own `daprd` that loads the shared `pubsub` component. |
| **Client integrations** | `AddAzureTableServiceClient("tables")` in the Players data project gets its connection from the AppHost by name, and adds health checks and tracing. |
| **Startup ordering** | `WaitFor(messaging)` and `WaitFor(tables)` keep each API from starting before its dependencies are healthy. |
| **YARP gateway** | `AddYarp("gateway")` builds its routes in code from a validated table ([`GatewayRoutes.cs`](src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/GatewayRoutes.cs)), so the browser talks to one origin. |
| **JavaScript apps** | `AddJavaScriptApp("frontend", ..., "start:aspire")` runs `ng serve` on a port Aspire assigns and passes it the gateway URL. |
| **Environment wiring** | `WithEnvironment(...)` passes endpoints between resources: the frontend gets the gateway URL, and each API gets the frontend origin for CORS. |
| **Service defaults** | [`ServiceDefaults`](src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.ServiceDefaults/Extensions.cs) adds OpenTelemetry, health endpoints, resilience and service discovery to every API with one `AddServiceDefaults()` call. |
| **Testable AppHost logic** | The gateway route rules and CORS wiring are unit tested in `HexMaster.Battleship.Aspire.Tests`. |

---

## Architecture

<p align="center">
<img src="docs/assets/architecture.svg" alt="Architecture: the browser loads the Angular frontend and calls the YARP gateway, which routes /users to players-api and /games to games-api. Each API has a Dapr sidecar publishing to the pubsub component backed by RabbitMQ. players-api stores data in Azurite tables. events-api streams server-sent events straight to the browser." width="100%">
</p>

**Request path.** The browser only knows relative URLs such as `/api/users/me`. An Angular
interceptor rewrites them to the gateway, and YARP sends each first path segment to the API
that owns it:

| Prefix | Module | API resource |
|---|---|---|
| `/users` | Players | `hexmaster-battleship-players-api` |
| `/games` | Games | `hexmaster-battleship-games-api` |

Platform paths such as `health`, `alive`, `openapi` and `dapr` are reserved and never routed, so
they stay internal. The route table is validated when the AppHost starts: a duplicate, reserved
or malformed prefix stops the AppHost with a message naming the module.

**Event path.** Services never call each other. They publish integration events through their
Dapr sidecar to topic `integration-events` on the RabbitMQ-backed `pubsub` component. The
**Events API** subscribes to that topic and pushes each event to the browsers it targets over
server-sent events.

---

## Your first tour of the dashboard

| Resource | Kind | What to look at |
|---|---|---|
| `frontend` | JavaScript app | The game. Click its endpoint. |
| `gateway` | YARP | The single backend origin the browser calls. |
| `hexmaster-battleship-players-api` | Project | Anonymous identity and tokens. Its traces show every token call. |
| `hexmaster-battleship-games-api` | Project | Game lifecycle (in progress). |
| `hexmaster-battleship-events-api` | Project | Server-sent events stream. |
| `*-dapr-cli` | Dapr sidecar | Sidecar logs: the `pubsub` component loading and every subscription. |
| `messaging` | Container | RabbitMQ. Its `management` URL opens the management UI. The user is `rabbitmq-username` (default `guest`) and the password is the generated `rabbitmq-password`; both appear in the resource details. |
| `storage` / `tables` | Azurite | Users and refresh tokens. |

Open **Traces** and create a player in the app. You'll see one trace go from the gateway into the
Players API and down to table storage.

---

## Repository layout

```text
battleship/
├── src/
│   ├── Battleship.slnx                   the solution
│   ├── Aspire/HexMaster.Battleship.Aspire/
│   │   ├── *.AppHost/                    AppHost.cs, GatewayRoutes.cs, DaprPrerequisites.cs
│   │   ├── *.ServiceDefaults/            OpenTelemetry, health, resilience, auth, CORS
│   │   └── *.Tests/                      tests for the AppHost logic
│   ├── Core/                             CQRS and domain building blocks + integration event catalog and sender
│   ├── Players/                          anonymous identity: module, Api, Data, Abstractions, Tests
│   ├── Games/                            game lifecycle: module, Api, Abstractions, Tests
│   ├── Events/                           SSE fan-out: module, Api, Abstractions, Tests
│   └── Frontend/                         Angular 22 + Angular Material, zoneless, signals
├── docs/
│   ├── design-system.md / .html          the visual contract (tokens, components, instruments)
│   └── assets/                           icon, banner, architecture diagram
├── openspec/                             specs and change proposals (spec-driven development)
├── .claude/  .github/                    agent skills, prompts and agents (Claude Code + Copilot)
└── CLAUDE.md                             working agreements for AI agents
```

Each backend module follows the same shape: a **module project** with vertical feature slices
under `Features/` (a command or query with its handler) and endpoint groups under `Endpoints/`, an
**Abstractions** project with the DTOs other modules may see, a thin **Api** host that calls
`AddServiceDefaults()`, and a **Tests** project (xUnit, Moq, Bogus).

---

## Services

### Players: anonymous identity

Visitors never register. The Players API gives each visitor an anonymous user with a signed
access token and an opaque refresh token. Every endpoint is under `/users`:

| Endpoint | Auth | Returns |
|---|---|---|
| `POST /users/tokens` | anonymous | `201` with a new user's token pair. The body `{ "displayName": "..." }` is optional; without it a nautical call sign is generated. |
| `POST /users/tokens/refresh` | anonymous | `200` with a new pair for `{ "refreshToken": "..." }`. The presented token is used up. `401` for an unknown, expired, used or revoked token. |
| `GET /users/me` | bearer access token | `200` with the caller's `id` and `displayName` |

[`HexMaster.Battleship.Players.Api.http`](src/Players/HexMaster.Battleship.Players.Api/HexMaster.Battleship.Players.Api.http)
has ready-made requests. The frontend keeps the refresh token in `localStorage`, so a returning
visitor is recognised, and the access token in `sessionStorage`, so it ends with the browser
session.

<details>
<summary><b>Storage, signing key and token lifetimes</b></summary>

#### Storage: Azurite

Users and refresh tokens live in Azure Table Storage (tables `users` and `refreshtokens`, created
on first use). Locally the `storage` resource is the
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) emulator with a
persistent lifetime: it keeps running, and keeps its data, when the AppHost stops. To start from
empty tables, remove the container:

```shell
docker ps --filter ancestor=mcr.microsoft.com/azure-storage/azurite   # shows its storage-... name
docker rm -f <name>
```

#### Signing key

Access tokens are HS256 JWTs signed with `Players:Tokens:SigningKey`, a string of at least 32
bytes. Every API that validates tokens reads the same key. An API that has no key, or a shorter
one, refuses to start and names the missing setting.

Under `aspire run` the key is the AppHost parameter `players-signing-key`. It is generated on
the first run and stored in the AppHost's user secrets, so tokens stay valid across restarts. To
choose the key yourself:

```shell
dotnet user-secrets set "Parameters:players-signing-key" "<at least 32 characters>" \
  --project src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost
```

To run the Players API on its own (`dotnet run` without the AppHost), give it the key and a
table storage connection through its own user secrets, with Azurite on its default ports:

```shell
docker run -d -p 10002:10002 mcr.microsoft.com/azure-storage/azurite azurite-table --tableHost 0.0.0.0
dotnet user-secrets set "Players:Tokens:SigningKey" "<at least 32 characters>" --project src/Players/HexMaster.Battleship.Players.Api
dotnet user-secrets set "ConnectionStrings:tables" "UseDevelopmentStorage=true" --project src/Players/HexMaster.Battleship.Players.Api
```

#### Token lifetimes

| Key | Default | Meaning |
|---|---|---|
| `Players:Tokens:AccessTokenLifetime` | `00:15:00` | How long an access token is valid |
| `Players:Tokens:RefreshTokenLifetime` | `90.00:00:00` | How long a refresh token is valid; it is replaced on every refresh |
| `Players:Tokens:ClockSkew` | `00:00:30` | Tolerance when checking expiry (at most 30 seconds) |
| `Players:Tokens:Issuer` | `battleship-players` | `iss` claim of every access token |
| `Players:Tokens:Audience` | `battleship` | `aud` claim of every access token |

</details>

### Games: the game itself

Creating a game, joining it with a code, placing ships, taking turns and ending the game. The
module and its API are in place. The features are planned as linked OpenSpec changes, listed in
the [Roadmap](#roadmap).

### Events: real time to the browser

The Events API (Dapr app id `events-api`) subscribes to topic `integration-events` on the
`pubsub` component and serves a server-sent events stream at `GET /events?playerId=<id>`. Each
message carries:

- `id`: a per-process sequence number
- `event`: the envelope type
- `data`: the envelope payload

After 15 idle seconds the stream writes a `heartbeat` without an id. A client that reconnects
with `Last-Event-ID` first receives the events it missed, from a replay buffer.

Publishers put one envelope on the topic, and its `target` decides who receives it: `player`
(the listed players), `game` (the listed participants) or `all` (everyone).

```json
{
  "id": "e-1",
  "type": "shot-fired",
  "target": "player",
  "targetIds": ["A"],
  "occurredAt": "2026-10-13T10:00:00Z",
  "payload": { "x": 3, "y": 5 }
}
```

<details>
<summary><b>Try it by hand</b></summary>

While the AppHost runs, hold a stream open for player `A`:

```shell
curl -N "http://localhost:5317/events?playerId=A"
```

and publish from a second terminal:

```shell
dapr publish --publish-app-id events-api --pubsub pubsub --topic integration-events \
  --data '{"id":"e-1","type":"shot-fired","target":"player","targetIds":["A"],"occurredAt":"2026-10-13T10:00:00Z","payload":{"x":3,"y":5}}'
```

The curl terminal prints `event: shot-fired`, `data: {"x":3,"y":5}` and `id: 1`. An envelope
targeting another player is not printed. Invalid envelopes are logged and dropped.

</details>

<details>
<summary><b>Events API configuration</b></summary>

| Key | Default | Meaning |
|---|---|---|
| `Events:PubSub:Name` | `pubsub` | Dapr pub/sub component to subscribe to |
| `Events:PubSub:Topic` | `integration-events` | Topic carrying the envelopes |
| `Events:AllowedOrigins` | `["http://localhost:4200"]` | Browser origins allowed to open `/events` |
| `Events:Stream:HeartbeatInterval` | `00:00:15` | Idle time before a heartbeat is written |
| `Events:Stream:ChannelCapacity` | `256` | Events queued per connection before the oldest is dropped |
| `Events:Replay:Capacity` | `1000` | Events kept for reconnecting clients |
| `Events:Replay:MaxAge` | `00:02:00` | Maximum age of a replayable event |

The Events API keeps its connections and replay buffer in process memory, so it runs as a
single instance. A second instance would only reach the browsers connected to it.

</details>

### Integration events

Backend services publish typed events through `IIntegrationEventSender`, which
`AddServiceDefaults()` registers, and always state who receives each one:

```csharp
await sender.SendAsync(
    new GameJoinedEvent(game.Id, game.Code, player.Id, player.Name),
    EventAudience.Game(game.PlayerIds),
    cancellationToken);
```

Type names are versioned (`game.shot-fired.v1`) and never change incompatibly. The catalog,
the versioning rules and the frontend sync step are in the
[integration events README](src/Core/HexMaster.Battleship.IntegrationEvents/README.md).

### Frontend

Angular 22 with Angular Material, zoneless and signal-driven. Each component keeps its data in
a local immutable state store and exposes it as read-only signals. The app never contains a
host or port. It calls `/api/<prefix>/...`, and an interceptor sends that to the gateway URL,
which `scripts/aspire-start.mjs` writes to `public/config.json` at start-up. Details are in
[`src/Frontend/README.md`](src/Frontend/README.md).

---

## Design system

<img src="docs/assets/battleship-icon.svg" alt="Battleship icon" width="72" align="right">

The UI is styled after a warship's bridge: deep ocean surfaces, cyan instrument light, and glow
instead of gloss. The fonts are **Quantico** for headings, **Courier Prime** for body text and
readouts, and **Bitcount Single** for mission text. Every colour, size and effect is a
`--bs-*` CSS custom property.

- [`docs/design-system.md`](docs/design-system.md): the visual contract, including tokens,
  components, game elements and instruments
- [`docs/design-system.html`](docs/design-system.html): the living showcase that renders every
  token

| Token | | Value | Role |
|---|---|---|---|
| `--bs-color-bg` | ![](https://img.shields.io/badge/-%20%20%20%20-011627?style=flat-square) | `#011627` | Background, the open sea |
| `--bs-color-primary` | ![](https://img.shields.io/badge/-%20%20%20%20-00c0ff?style=flat-square) | `#00c0ff` | Accent, instruments, radar |
| `--bs-color-secondary` | ![](https://img.shields.io/badge/-%20%20%20%20-2f87ff?style=flat-square) | `#2f87ff` | Ships, secondary actions |
| `--bs-color-danger` | ![](https://img.shields.io/badge/-%20%20%20%20-ff2146?style=flat-square) | `#ff2146` | Hits, destructive actions |
| `--bs-color-text` | ![](https://img.shields.io/badge/-%20%20%20%20-b2c2d4?style=flat-square) | `#b2c2d4` | Body text |

---

## Testing

```shell
dotnet test src/Battleship.slnx      # every backend test project
cd src/Frontend && npm test           # Vitest + axe accessibility checks
```

> [!NOTE]
> The repository and API tests in `HexMaster.Battleship.Players.Tests` need table storage. They
> use Azurite on its default ports (`UseDevelopmentStorage=true`), or the connection string in
> `BATTLESHIP_TESTS_TABLES`. When neither answers, they are skipped with a reason. To run them
> against the emulator that `aspire run` started, point the variable at its table port, which
> the `storage` resource shows on the dashboard.

---

## How we work

**Spec-driven with [OpenSpec](https://github.com/Fission-AI/OpenSpec).** Every feature starts as
a change under `openspec/changes/<name>/`, with a proposal, a design, delta specs and tasks.
After it ships, the change is archived into `openspec/specs/`, which describes the system as it
is.

**Git flow.**

- `main` holds releases, and `development` is the integration branch.
- A change is proposed on `development`.
- It is implemented in its own worktree at `../battleship-worktrees/<change>`, on branch
  `change/<change>`, with a commit per task.
- It is merged through a pull request into `development`, then archived.

The full rules are in [`openspec-git-flow`](.claude/skills/openspec-git-flow/SKILL.md).

**Issues ↔ changes.** A GitHub issue is triaged into a linked change (`/issue`), and the issue
stays in sync through implementation and archive.

**AI-assisted, on both sides.** The repository is set up for Claude Code and GitHub Copilot
alike. Skills, prompts and agents are mirrored between `.claude/` and `.github/`:

- coding standards: .NET 10, Minimal APIs, CQRS, vertical slices, xUnit/Moq/Bogus
- Aspire workflows
- the gateway route table (`yarp-gateway-routes`)
- an Angular frontend agent

[`CLAUDE.md`](CLAUDE.md) holds the working agreements.

### Roadmap

| Change | Issue | Status |
|---|---|---|
| Anonymous user identification | [#7](https://github.com/hexmasternl/battleship/issues/7) | ✅ merged |
| Create a new game | [#13](https://github.com/hexmasternl/battleship/issues/13) | 📝 proposed |
| Join a game | [#16](https://github.com/hexmasternl/battleship/issues/16) | 📝 proposed |
| Placement phase | [#17](https://github.com/hexmasternl/battleship/issues/17) | 📝 proposed |
| Game play page | [#18](https://github.com/hexmasternl/battleship/issues/18) | 📝 proposed |
| End of the game | [#19](https://github.com/hexmasternl/battleship/issues/19) | 📝 proposed |

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| AppHost exits with *"Dapr prerequisite missing"* | Install the Dapr CLI and/or run `dapr init`, then start again. |
| Sidecars keep restarting | Check that Docker is running and that `dapr init` finished, then read the `*-dapr-cli` console logs on the dashboard. |
| Every `/api/` call fails in the browser | You started `ng serve` on its own. Start through `aspire run`, so `public/config.json` points at the gateway. |
| Old players are still there | Azurite is persistent on purpose. Remove its container to reset (see [Players](#players-anonymous-identity)). |
| An API refuses to start over the signing key | `Players:Tokens:SigningKey` is missing or shorter than 32 bytes. Under Aspire, check the `players-signing-key` parameter. |

---

<div align="center">

<img src="docs/assets/battleship-icon.svg" alt="" width="48">

<sub>Released under the <a href="LICENSE">MIT license</a> · Built by <a href="https://github.com/hexmasternl">HexMaster</a> with Aspire, Dapr and Angular</sub>

</div>
