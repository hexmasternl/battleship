# Prerequisites

Battleship runs on your own machine with one command. Before that command works, five tools need to be installed. None of them needs a cloud account, and you never write a connection string or a `docker-compose.yml` by hand: the Aspire AppHost starts and wires everything for you.

## What to install

| Tool | Why this app needs it | Check that it works |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Builds and runs the AppHost and the C# services. | `dotnet --version` |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/), or another container runtime | Runs the RabbitMQ and Azurite containers. `dapr init` starts its own containers in it too. Keep it running. | `docker version` |
| [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) | Starts the Dapr sidecars next to the services. | `dapr --version` |
| [Node.js](https://nodejs.org/en/download) with npm | Runs the Angular dev server for the frontend. npm comes with Node.js. | `node --version` |
| [Aspire CLI](https://aspire.dev/get-started/install-cli/) | Builds the solution, starts the AppHost and opens the dashboard. | `aspire --version` |

Docker Desktop is the runtime this repository is tested with. Aspire can also use another runtime, such as Podman, but `dapr init` then needs its `--container-runtime` option.

## One-time setup

Two steps prepare the machine and the repository. You only run them once.

```shell
dapr init                              # downloads daprd and starts the Dapr placement and scheduler containers
cd src/Frontend && npm install && cd -  # installs the frontend packages
```

[`dapr init`](https://docs.dapr.io/getting-started/install-dapr-selfhost/) installs the Dapr runtime binary, `daprd`, under `.dapr/bin` in your home folder. It also starts Redis and Zipkin containers. This app does not use them, and they do not conflict with the containers the AppHost starts.

## Start the AppHost

From the repository root:

```shell
cd src/Aspire/HexMaster.Battleship.Aspire
aspire run
```

The Aspire CLI builds the solution, starts the AppHost, prints the dashboard URL and opens the dashboard in your browser. When every resource shows **Running**, open the endpoint of the `frontend` resource and play. To stop, press `Ctrl+C`, or run `aspire stop` from the same folder.

What the AppHost starts is described in [Resources](resources.md).

## The fail-fast Dapr check

Without Dapr, the sidecar resources would fail and restart on the dashboard over and over without saying why. So before the AppHost starts any resource, it checks for Dapr itself:

```csharp
// AppHost.cs
if (builder.ExecutionContext.IsRunMode)
{
    DaprPrerequisites.EnsureInstalled();
}
```

[`DaprPrerequisites.cs`](../src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/DaprPrerequisites.cs) makes two checks and stops the AppHost with a message naming the missing step:

1. **The Dapr CLI is installed.** It looks for `dapr` (`dapr.exe` on Windows) on the `PATH` and in the places the Dapr installer uses. If it is missing, the message is *"Dapr prerequisite missing: the Dapr CLI was not found."*, with the install link and the advice to run `dapr init`.
2. **`dapr init` has run.** It looks for the `daprd` runtime under `.dapr/bin` in your home folder. If it is missing, the message is *"Dapr prerequisite missing: the Dapr runtime is not initialised"*, with the advice to run `dapr init`.

Install what is missing, run `dapr init`, and start the AppHost again.

> If the sidecars keep restarting even though the check passed, make sure Docker is running and that `dapr init` finished. Then read the console logs of the `*-dapr-cli` resources on the dashboard.
