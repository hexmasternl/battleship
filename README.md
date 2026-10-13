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
