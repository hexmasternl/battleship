# Proposal

> Tracks GitHub issue [hexmasternl/battleship#81](https://github.com/hexmasternl/battleship/issues/81).

## Why

Running Battleship locally takes five tools installed by hand: the .NET 10 SDK, Docker, the Dapr CLI with `dapr init`, Node.js with npm, and the Aspire CLI. The repository workflow adds more: `gh`, the OpenSpec CLI, the Azure CLI with Bicep, and Playwright. Every contributor installs these per machine and OS, versions drift away from CI, and the AppHost needs a fail-fast Dapr check (`DaprPrerequisites.cs`) because the setup goes wrong often enough. A dev container turns the whole setup into "Reopen in Container", so that `aspire run` starts the full game on any machine that has only Docker and VS Code.

## What Changes

- **A dev container definition** in `.devcontainer/devcontainer.json`, based on the official Aspire dev container template (Ubuntu base image, the Aspire Dev Container Feature, Docker-in-Docker), extended with the Dapr CLI, .NET 10, Node.js 24, the GitHub CLI, the Azure CLI with Bicep, and PowerShell as Dev Container Features.
- **Lifecycle scripts** in `.devcontainer/` that restore the solution, install the frontend packages, install the OpenSpec CLI and the Playwright Chromium browser, run `dapr init` once Docker is running, and trust the Aspire development certificate on every start. They are idempotent and name the step that failed.
- **Docker-in-Docker** for every container the app needs: RabbitMQ, Azurite, the Dapr placement and scheduler containers, and the containers the Integration tests start through `Aspire.Hosting.Testing`. Endpoints, sidecars and connection strings then resolve on `localhost` as they do on a developer machine. Testcontainers works the same way.
- **Named volumes** so a rebuild keeps the Docker data (images, the persistent Azurite container and its users and games), the AppHost user secrets (generated `rabbitmq-password` and `users-signing-key`), the initialised Dapr runtime, the NuGet cache, the npm cache, `src/Frontend/node_modules`, and the container-side change worktrees.
- **Change worktrees inside the container** live in a named volume at `/workspaces/battleship-worktrees`, so `../battleship-worktrees/<change>` from the openspec git flow resolves there. The docs explain that worktrees are created and used on one side only (host or container), and that `git worktree prune` must not run on the other side.
- **Editor setup** for both agent setups in this repository: C# Dev Kit, the Aspire extension, Angular Language Service, Dapr, Bicep, Docker, GitHub Actions, GitHub Copilot Chat, and Claude Code.
- **CI** builds the dev container on pull requests that touch `.devcontainer/**` and runs a smoke check inside it (tool versions, solution build, unit tests). Dependabot gains the `devcontainers` ecosystem.
- **Documentation**: `README.md` Quick start and `docs/prerequisites.md` (also served at `/help/prerequisites`) present the dev container as the recommended route and keep the manual install as the alternative. `CONTRIBUTING.md` points at it too.

GitHub Codespaces is out of scope. There, the browser reaches the app on `*.app.github.dev` origins, which the frontend runtime configuration and the API CORS policies do not support today. It deserves its own issue.

## Capabilities

### New Capabilities

- `dev-container`: the containerised local development environment: which tools it provides, which state survives a rebuild, how Docker, Dapr and port forwarding behave inside it, how change worktrees work there, and how CI keeps it buildable.

### Modified Capabilities

- `local-messaging`: the documented prerequisites requirement also names the dev container as a way to get Docker, the Dapr CLI and an initialised Dapr runtime without installing them on the host.
- `help-section`: the prerequisites topic also explains the dev container route, next to the list of tools for a manual install.

## Impact

- New: `.devcontainer/devcontainer.json`, `.devcontainer/post-create.sh`, `.devcontainer/post-start.sh`, `.gitattributes` (LF for `*.sh`, so a Windows checkout does not break the scripts).
- `.github/workflows/`: a dev container build and smoke-check job. `.github/dependabot.yml`: the `devcontainers` ecosystem.
- Docs: `README.md`, `docs/prerequisites.md`, `CONTRIBUTING.md`. `src/Frontend/src/app/help/help-docs-content.spec.ts` gains an assertion for the new prerequisites section.
- No change to service code, the AppHost resource graph, `DaprPrerequisites.cs`, production images or `infra/`. The AppHost's Dapr check must pass inside the container unchanged.
- Host requirement: Docker Desktop (WSL2 on Windows) or Docker Engine with enough memory for three APIs, three sidecars, RabbitMQ, Azurite, the Dapr containers and `ng serve`. Docker-in-Docker runs the container privileged.
