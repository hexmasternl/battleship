# Design

## Context

See proposal.md for the motivation. Facts that shape the approach:

- The AppHost (Aspire SDK 17.0.0) starts RabbitMQ and a **persistent** Azurite container. It runs three APIs, each with a Dapr sidecar from the Community Toolkit, plus the YARP gateway, the Angular dev server (`start:aspire` binds `ng serve` to `localhost` on the port Aspire assigns) and the dev bot.
- `DaprPrerequisites.EnsureInstalled()` looks for `dapr` on `PATH` (or `/usr/local/bin`, `~/dapr`) and for `~/.dapr/bin/daprd`. It must pass unchanged.
- The AppHost stores generated parameters (`rabbitmq-password`, `users-signing-key`) in user secrets under `~/.microsoft/usersecrets/<UserSecretsId>`.
- The frontend's runtime `config.json` points at `http://localhost:<gateway port>`, and the APIs allow the `http://localhost:<frontend port>` origin for CORS. Both work from a host browser only if the forwarded port number is the same on the host.
- CI uses .NET `10.0.x`, Node `24` and `dapr init --slim`. It runs backend tests without the `Integration` category, with Azurite as a service container (`BATTLESHIP_TESTS_TABLES=UseDevelopmentStorage=true`). The storage tests skip themselves when no Azurite answers.
- The repository has no `.gitattributes`. A Windows checkout with `core.autocrlf=true` would write shell scripts with CRLF line endings, which breaks them in Linux.
- Microsoft publishes an official Aspire dev container template (`microsoft/aspire-devcontainer`) and the feature `ghcr.io/microsoft/aspire-devcontainer-feature/aspire`. The feature installs the stable Aspire CLI and the Aspire VS Code extension, and turns on hybrid auto port forwarding. The template adds Docker-in-Docker, .NET and Node features, puts `~/.aspnet/dev-certs/trust` on `SSL_CERT_DIR`, and runs `aspire certs trust --non-interactive` on start.
- Dapr publishes the feature `ghcr.io/dapr/cli/dapr-cli`.

## Goals / Non-Goals

**Goals:**
- Follow the official Aspire template so the setup stays recognisable and easy to update, and add only what this repository needs.
- Keep host-side and container-side runs alike, so the docs, the AppHost and the tests need no container-specific branches.

**Non-Goals:**
- GitHub Codespaces support (proposal). The definition must not break in Codespaces, but nothing is tuned or tested for it.
- Removing manual installation as a supported route.
- Changing what CI uses to build and test the product. CI keeps its `setup-*` actions; only the new smoke job uses the container.

## Decisions

### 1. Base image and features: the Aspire template plus repository extras

`image: mcr.microsoft.com/devcontainers/base:ubuntu-24.04` with these features:

| Feature | Options | Why |
|---|---|---|
| `ghcr.io/devcontainers/features/dotnet:2` | `version: 10.0` | SDK for the solution |
| `ghcr.io/microsoft/aspire-devcontainer-feature/aspire:3` | defaults | Aspire CLI, extension, port-forward settings |
| `ghcr.io/devcontainers/features/docker-in-docker:2` | `moby: true` | containers of the run |
| `ghcr.io/dapr/cli/dapr-cli:0` | defaults | Dapr CLI |
| `ghcr.io/devcontainers/features/node:1` | `version: 24` | matches CI |
| `ghcr.io/devcontainers/features/github-cli:1` | defaults | `gh` for the issue and git flow skills |
| `ghcr.io/devcontainers/features/azure-cli:1` | `installBicep: true` | `infra/` |
| `ghcr.io/devcontainers/features/powershell:1` | defaults | scripts and docs that use `pwsh` |
| `ghcr.io/anthropics/devcontainer-features/claude-code:1` | defaults | Claude Code CLI |

The npm version from `packageManager` (`npm@12.2.0`) is installed in `post-create` with `npm i -g npm@<version>`, read from `package.json` so it never drifts. The OpenSpec CLI (`@fission-ai/openspec`) is installed globally in `post-create` at the version the repository uses (1.14.0 at the time of writing). Playwright's Chromium comes from `npx playwright install --with-deps chromium`.

*Alternatives*: the `mcr.microsoft.com/devcontainers/dotnet` image (fewer features but diverges from the Aspire template), or a custom Dockerfile (more control, but we would maintain every install step ourselves and lose Dependabot updates for features). Features win on maintenance.

### 2. Docker-in-Docker, not Docker-outside-of-Docker

Every container (RabbitMQ, Azurite, Dapr placement and scheduler, test containers) runs on a Docker engine inside the dev container. Endpoints, sidecar addresses and the Azurite connection string then resolve on `localhost` exactly as on a developer machine.

*Alternative*: mounting the host's Docker socket. That is lighter, but the published ports then belong to the host, so `localhost` from inside the container misses them. Aspire, the Dapr sidecars, Testcontainers and `Aspire.Hosting.Testing` would all need host-address overrides. Rejected.

### 3. Full `dapr init`, run after Docker is up

`post-create` waits up to 60 seconds for `docker info` to succeed, then runs `dapr init` unless `~/.dapr/bin/daprd` already exists. Full init matches the docs, which say `dapr init` starts placement and scheduler. `post-start` restarts the `dapr_placement` and `dapr_scheduler` containers if they exist but are stopped, because the Docker engine starts empty after a container restart.

*Alternative*: `--slim`, as in CI. It avoids four containers, but it diverges from the documented local setup, and actors, workflows and the scheduler would not be available if the app ever uses them. Full init keeps one story. The Redis and Zipkin containers that `dapr init` starts are stopped in `post-create` to save memory. Nothing uses them, and the sidecars get their tracing configuration from the AppHost.

### 4. Named volumes for everything that must survive a rebuild

| Volume | Mount target |
|---|---|
| `battleship-docker` | `/var/lib/docker` (images, the persistent Azurite container and its data, Dapr containers) |
| `battleship-usersecrets` | `/home/vscode/.microsoft/usersecrets` |
| `battleship-dapr` | `/home/vscode/.dapr` |
| `battleship-nuget` | `/home/vscode/.nuget/packages` |
| `battleship-npm` | `/home/vscode/.npm` |
| `battleship-node-modules` | `${containerWorkspaceFolder}/src/Frontend/node_modules` |
| `battleship-worktrees` | `/workspaces/battleship-worktrees` |

Volume mounts are created root-owned, so `post-create` runs `sudo chown -R vscode:vscode` on each user-level target before it writes to them.

`node_modules` in a volume keeps Linux-native binaries (esbuild, the Angular compiler) away from the host's `node_modules`, and avoids slow bind-mount I/O on Windows and macOS.

### 5. Change worktrees in a named volume

The workspace is mounted at `/workspaces/battleship`, so `../battleship-worktrees` resolves to `/workspaces/battleship-worktrees`, which is the `battleship-worktrees` volume. No change to the git flow skill is needed. Git stores absolute paths in worktree metadata, so a worktree is used only on the side that created it. The docs warn not to run `git worktree prune` on the other side, because it would drop the metadata of worktrees it cannot see. Chosen by the user over bind-mounting the host folder.

`post-create` adds the workspace to git's `safe.directory`, because the bind-mounted repository is owned by a different UID on some hosts.

### 6. Certificates and port forwarding

Follow the Aspire template: `remoteEnv.SSL_CERT_DIR` includes `~/.aspnet/dev-certs/trust`, and `postStartCommand` runs `aspire certs trust --non-interactive`. Tools inside the container (curl, the dev bot, Playwright) then trust the run's HTTPS endpoints. The host browser does not trust the container's certificate and shows a warning once for the dashboard. The docs say so.

Forwarding: the Aspire feature turns on hybrid auto-forwarding, and Aspire's dev container support forwards dashboard and resource ports. `portsAttributes` labels the dashboard port (17150 in the `https` launch profile). VS Code forwards to the same port number on the host when it is free, which keeps the `localhost` URLs in `config.json` and the CORS origins valid. The docs explain what happens when a host port is taken (VS Code picks another port, and the browser origin then differs) and how to fix it: free the port, or forward it manually to the same number.

### 7. Line endings

Add `.gitattributes` with `*.sh text eol=lf`, plus `* text=auto`, so the scripts work from any checkout. The scripts start with `set -euo pipefail` and a `step "<name>"` helper. On error, a trap prints `dev container setup failed at step: <name>`.

### 8. Lifecycle split

- `onCreateCommand`: none. Docker is not up during prebuild-style phases, and the work is quick enough for post-create.
- `postCreateCommand: bash .devcontainer/post-create.sh`: fix volume ownership, set git `safe.directory`, install the npm version and the OpenSpec CLI, run `dotnet restore src/Battleship.slnx`, run `npm ci` in `src/Frontend` (skipped when the lockfile hash stored in `node_modules/.devcontainer-lock` matches), install the Playwright Chromium, wait for Docker, run `dapr init`, stop the Redis and Zipkin containers. Each step is a guarded no-op on re-run.
- `postStartCommand: bash .devcontainer/post-start.sh`: wait for Docker, restart the Dapr containers, run `aspire certs trust --non-interactive`, and print the next step (`cd src/Aspire/HexMaster.Battleship.Aspire && aspire run`).

### 9. Container-backed tests

`dotnet test` runs as in CI. The storage tests need Azurite on the standard ports (`UseDevelopmentStorage=true`) and skip themselves when none answers. The docs show the one-liner to start one inside the container (`docker run -d --name azurite-tests -p 10000-10002:10000-10002 mcr.microsoft.com/azure-storage/azurite`), or how to point `BATTLESHIP_TESTS_TABLES` at the emulator of a running `aspire run`. The `Integration` category runs in the container because Docker and Dapr are present.

### 10. CI smoke job and Dependabot

A new workflow, `.github/workflows/devcontainer.yml`, triggers on pull requests and pushes to `development` and `main` that touch `.devcontainer/**` or `.gitattributes`, and on `workflow_dispatch`. It uses `devcontainers/ci` to build the container and run `.devcontainer/smoke-check.sh` inside it. The script prints every tool version, checks `~/.dapr/bin/daprd`, builds the solution in Release, and runs the backend tests without the `Integration` category. Frontend unit tests are not run there, because the regular CI job already covers them on the same Node major. The image is not pushed to a registry.

Dependabot gains `package-ecosystem: devcontainers` with `directory: /`, a weekly schedule, `target-branch: development` and the `chore(deps)` prefix, like the existing entries.

### 11. Documentation

- `README.md` Quick start: a new "Option A: dev container (recommended)" before the manual install, which becomes "Option B".
- `docs/prerequisites.md` (served at `/help/prerequisites`): a "Use the dev container" section with what the host needs, what the container provides, the first-run time, the certificate warning, port forwarding, the worktree caveat and the test Azurite command. `help-docs-content.spec.ts` asserts the section exists.
- `CONTRIBUTING.md`: one paragraph that points at the dev container.

## Risks / Trade-offs

- [Docker-in-Docker runs the container privileged] → Documented; it is the official Aspire recommendation for AppHosts with container resources, and the container holds no secrets.
- [Memory: about 10 containers plus `ng serve` and three .NET processes] → Stop Redis and Zipkin; document at least 8 GB memory for Docker Desktop (WSL2) and 16 GB recommended.
- [Host port taken, so VS Code forwards to another port and the browser origin breaks CORS and `config.json`] → Documented with the fix. Making the app origin-agnostic belongs to the Codespaces follow-up.
- [The Aspire feature installs the latest stable CLI, which can run ahead of the SDK in the AppHost] → The CLI is backwards compatible with older AppHosts. The smoke check prints both versions, and Dependabot keeps the SDK moving.
- [Worktree metadata mismatch between host and container] → Documented; the volume keeps container worktrees apart.
- [First creation is slow (images, restore, Chromium, Dapr init)] → The volumes make every later rebuild fast. The docs state the expected first-run time.
- [`ghcr.io/dapr/cli/dapr-cli` and the Claude Code feature are maintained outside Microsoft's dev container org] → Pinned by major version and updated through Dependabot; the smoke check fails if a tool is missing.

## Migration Plan

Nothing to migrate. The dev container is additive and the manual route keeps working. Rollback is removing `.devcontainer/`, the workflow and the Dependabot entry.
