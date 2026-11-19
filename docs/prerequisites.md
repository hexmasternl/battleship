# Prerequisites

Battleship runs on your own machine with one command. Before that command works, five tools need to be installed. None of them needs a cloud account, and you never write a connection string or a `docker-compose.yml` by hand: the Aspire AppHost starts and wires everything for you.

You can skip the installation entirely with the repository's dev container, described next. The rest of this page is for installing the tools on your own machine.

## Use the dev container

The repository has a dev container in `.devcontainer/`: a Linux container that brings every tool below, plus the tools the repository workflow uses, with `dapr init` already done.

**What your machine needs**: Docker Desktop (with the WSL 2 backend on Windows) or Docker Engine, and VS Code with the Dev Containers extension. Give Docker at least 8 GB of memory; 16 GB is comfortable. The app runs three APIs, three Dapr sidecars, RabbitMQ, Azurite, the Dapr containers and the Angular dev server.

**What the container provides**:

| Tool | Notes |
|---|---|
| .NET 10 SDK, Aspire CLI | The Aspire VS Code extension is installed too. |
| Docker | Its own engine inside the container (Docker-in-Docker), so RabbitMQ, Azurite and the Dapr containers run there and answer on `localhost`, as on a developer machine. |
| Dapr CLI | `dapr init` has run. The unused Redis and Zipkin containers it starts are stopped. |
| Node.js 24 with npm | The npm version from `src/Frontend/package.json`. Frontend packages are installed. |
| GitHub CLI, OpenSpec CLI | For the issue and change workflow. |
| Azure CLI with Bicep, PowerShell | For `infra/`. |
| Playwright with Chromium | For headless checks of the frontend. |
| Claude Code, GitHub Copilot Chat | Both agent setups of this repository. |

**Start it**: open the repository in VS Code and run **Dev Containers: Reopen in Container**. The first build takes five to ten minutes: it downloads the images, restores the packages, installs Chromium and initialises Dapr. Later starts take seconds. Then start the AppHost in the container's terminal:

```shell
cd src/Aspire/HexMaster.Battleship.Aspire
aspire run
```

**Ports and the dashboard**: VS Code forwards the ports of the run to your machine, so open the dashboard link that `aspire run` prints, and the `frontend` endpoint, in your own browser. The frontend's runtime configuration and the APIs' allowed origins use the `localhost` port numbers from inside the container. They only work when VS Code forwards each port to the same number on your machine, which it does when that port is free. If a port is taken on your machine, stop whatever uses it, or forward the port by hand to the same number in the **Ports** view.

**Certificate warning**: tools inside the container trust the Aspire development certificate. Your browser on the host does not, so it warns once on the dashboard's HTTPS address. Check that the address is `localhost` and continue.

**What survives a rebuild**: named Docker volumes keep the AppHost user secrets (the generated RabbitMQ password and token signing key), the ASP.NET development certificate, the Docker data (images, and the Azurite data volume with your users and games), the Dapr runtime, the NuGet and npm caches, the frontend packages and the change worktrees. "Rebuild Container" takes about six minutes, most of it to reinstall Playwright's Chromium and its system packages, but it downloads no images again and your games are still there. To start from scratch, delete the volumes whose names start with `battleship-` and `dind-var-lib-docker-`.

**Change worktrees**: the change workflow creates worktrees in `../battleship-worktrees/<change>`. Inside the container that is a volume at `/workspaces/battleship-worktrees`. Git stores absolute paths for worktrees, so use a worktree only on the side that created it (host or container). Don't run `git worktree prune` on the other side: it would remove the metadata of worktrees it cannot see.

**Storage tests**: the Users and Games storage tests need Azurite on its standard ports and skip themselves when none answers. Start one in the container before `dotnet test`:

```shell
docker run -d --name azurite-tests -p 10000-10002:10000-10002 mcr.microsoft.com/azure-storage/azurite
```

Or set `BATTLESHIP_TESTS_TABLES` to the connection string of the emulator a running `aspire run` started. The `Integration` tests start the whole AppHost and work in the container as they do on your machine.

The setup scripts are `.devcontainer/post-create.sh` and `.devcontainer/post-start.sh`. Both can run again at any time, and when a step fails they print its name.

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
