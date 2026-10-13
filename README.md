# battleship
Battleship on Aspire

## Running locally

The Aspire AppHost starts every service, the RabbitMQ broker and a Dapr sidecar per
service. Install these once:

| Prerequisite | Why | Check |
|---|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running) | RabbitMQ runs as a container, and `dapr init` runs its own containers | `docker version` |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Builds and runs the services | `dotnet --version` |
| [Aspire CLI](https://aspire.dev/get-started/install-cli/) | Runs the AppHost and shows the dashboard | `aspire --version` |
| [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) | Starts the Dapr sidecars | `dapr --version` |
| Initialised Dapr runtime | Downloads `daprd` and starts the placement and scheduler containers | `dapr init` once; `dapr --version` then shows a runtime version |

`dapr init` also starts a Redis and a Zipkin container. They are not used by this solution
and do not conflict with the RabbitMQ container the AppHost starts.

Start the application from the Aspire folder:

```shell
cd src/Aspire/HexMaster.Battleship.Aspire
aspire run
```

The command builds the solution, starts the AppHost and prints the dashboard URL (it also
opens it in the browser). On the dashboard:

- `messaging` is the RabbitMQ broker. Its `management` URL opens the RabbitMQ management
  UI; the username is the `rabbitmq-username` parameter (default `guest`) and the password
  is the generated `rabbitmq-password` parameter, both shown under the resource's details.
- Each API has a Dapr sidecar resource (`<api>-dapr-cli`). Its console log is the sidecar
  log, which shows the `pubsub` component loading and every subscription.

If the AppHost stops immediately with a message about Dapr, the Dapr CLI is not installed or
`dapr init` has not been run on this machine. Run the step the message names and start again.

To stop everything, press `Ctrl+C` in the terminal that runs `aspire run`, or run
`aspire stop` from the same folder.

## Player identity

Visitors never register. The Players API gives each visitor an anonymous user with a signed
access token and an opaque refresh token. Every Players endpoint is under `/users`, which is
also the Players prefix on the gateway:

| Endpoint | Auth | Returns |
|---|---|---|
| `POST /users/tokens` | anonymous | `201` with a new user's token pair; the body `{ "displayName": "..." }` is optional (a nautical call sign is generated without it) |
| `POST /users/tokens/refresh` | anonymous | `200` with a new pair for `{ "refreshToken": "..." }`; the presented token is used up. `401` for an unknown, expired, used or revoked token |
| `GET /users/me` | bearer access token | `200` with the caller's `id` and `displayName` |

The frontend calls them as `/api/users/...`; its interceptor sends them to the gateway.
`src/Players/HexMaster.Battleship.Players.Api/HexMaster.Battleship.Players.Api.http` has
ready-made requests.

### Storage: Azurite

Users and refresh tokens live in Azure Table Storage (tables `users` and `refreshtokens`,
created on first use). Locally the AppHost runs the `storage` resource as the
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) emulator
container, so `aspire run` needs Docker but no Azure account. The container has a persistent
lifetime: it keeps running, and keeps its data, when the AppHost stops. To start from empty
tables, remove it (`docker ps --filter ancestor=mcr.microsoft.com/azure-storage/azurite`
shows its `storage-...` name, then `docker rm -f <name>`).

### Signing key

Access tokens are HS256 JWTs signed with `Players:Tokens:SigningKey`, a string of at least 32
bytes. Every API that validates tokens reads the same key, and an API that has no key, or a
shorter one, refuses to start and names the missing setting.

Under `aspire run` the key is the AppHost parameter `players-signing-key`. It is generated on
the first run and stored in the AppHost's user secrets, so tokens stay valid across restarts.
To choose the key yourself:

```shell
dotnet user-secrets set "Parameters:players-signing-key" "<at least 32 characters>" --project src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost
```

To run the Players API on its own (`dotnet run` without the AppHost), give it the key and a
table storage connection through its own user secrets, with Azurite listening on its default
ports:

```shell
docker run -d -p 10002:10002 mcr.microsoft.com/azure-storage/azurite azurite-table --tableHost 0.0.0.0
dotnet user-secrets set "Players:Tokens:SigningKey" "<at least 32 characters>" --project src/Players/HexMaster.Battleship.Players.Api
dotnet user-secrets set "ConnectionStrings:tables" "UseDevelopmentStorage=true" --project src/Players/HexMaster.Battleship.Players.Api
```

### Token lifetimes

| Key | Default | Meaning |
|---|---|---|
| `Players:Tokens:AccessTokenLifetime` | `00:15:00` | How long an access token is valid |
| `Players:Tokens:RefreshTokenLifetime` | `90.00:00:00` | How long a refresh token is valid; it is replaced on every refresh |
| `Players:Tokens:ClockSkew` | `00:00:30` | Tolerance when checking expiry (at most 30 seconds) |
| `Players:Tokens:Issuer` | `battleship-players` | `iss` claim of every access token |
| `Players:Tokens:Audience` | `battleship` | `aud` claim of every access token |

The frontend keeps the refresh token in `localStorage`, so a returning visitor is recognised,
and the access token in `sessionStorage`, so it ends with the browser session.

### Storage tests

The repository and API tests in `HexMaster.Battleship.Players.Tests` need table storage. They
use Azurite on its default ports (`UseDevelopmentStorage=true`), or the connection string in
the `BATTLESHIP_TESTS_TABLES` environment variable, and are skipped with a reason when neither
answers. To run them against the emulator `aspire run` started, point the variable at its
table port (shown on the `storage` resource on the dashboard).

## Real-time events

The Events API (`hexmaster-battleship-events-api`, Dapr app id `events-api`) pushes
integration events to browsers. It subscribes to topic `integration-events` on the `pubsub`
component and serves a server-sent events stream at `GET /events?playerId=<id>`. Each message
carries the per-process sequence number as `id`, the envelope type as `event` and the
envelope payload as `data`. A `heartbeat` message without an id is written after every 15
idle seconds, and a client that reconnects with `Last-Event-ID` first receives the events it
missed from the replay buffer.

Publishers put one envelope on the topic; the target decides who receives it:

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

`target` is `player` (the listed players), `game` (the listed participants) or `all`
(everyone; `targetIds` may be empty). Invalid envelopes are logged and dropped.

To try it while the AppHost runs, hold a stream open for player `A`:

```shell
curl -N "http://localhost:5317/events?playerId=A"
```

and publish from a second terminal:

```shell
dapr publish --publish-app-id events-api --pubsub pubsub --topic integration-events --data '{"id":"e-1","type":"shot-fired","target":"player","targetIds":["A"],"occurredAt":"2026-10-13T10:00:00Z","payload":{"x":3,"y":5}}'
```

The curl terminal prints `event: shot-fired`, `data: {"x":3,"y":5}` and `id: 1`. An envelope
targeting another player is not printed.

The Events API keeps its connections and replay buffer in process memory, so it runs as a
single instance. A second instance would only reach the browsers connected to it.

Configuration of the Events API:

| Key | Default | Meaning |
|---|---|---|
| `Events:PubSub:Name` | `pubsub` | Dapr pub/sub component to subscribe to |
| `Events:PubSub:Topic` | `integration-events` | Topic carrying the envelopes |
| `Events:AllowedOrigins` | `["http://localhost:4200"]` | Browser origins allowed to open `/events` |
| `Events:Stream:HeartbeatInterval` | `00:00:15` | Idle time before a heartbeat is written |
| `Events:Stream:ChannelCapacity` | `256` | Events queued per connection before the oldest is dropped |
| `Events:Replay:Capacity` | `1000` | Events kept for reconnecting clients |
| `Events:Replay:MaxAge` | `00:02:00` | Maximum age of a replayable event |
