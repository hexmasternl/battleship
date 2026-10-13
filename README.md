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
