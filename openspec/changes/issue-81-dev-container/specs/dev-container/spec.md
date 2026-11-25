# Spec Delta

## Purpose

Defines the containerised local development environment: a dev container that provides every tool needed to build, run and test Battleship and to follow the repository workflow, so a developer needs only Docker and a dev-container-capable editor on the host.

## ADDED Requirements

### Requirement: The dev container provides the full toolchain
The repository SHALL contain a dev container definition. In a container built from it, the .NET 10 SDK, the Aspire CLI, the Dapr CLI, a Docker engine, Node.js with npm, the GitHub CLI, the OpenSpec CLI, the Azure CLI with Bicep, and PowerShell SHALL be on `PATH` without manual installation. The .NET and Node.js major versions SHALL match the ones CI uses.

#### Scenario: Tools available after creation
- **WHEN** a developer opens the repository in the dev container for the first time and creation finishes
- **THEN** `dotnet`, `aspire`, `dapr`, `docker`, `node`, `npm`, `gh`, `openspec`, `az bicep` and `pwsh` each report a version

#### Scenario: Versions match CI
- **WHEN** `dotnet --version` and `node --version` run in the container
- **THEN** they report .NET 10 and the Node.js major version the CI workflows set up

### Requirement: The dev container is ready to run the app after creation
When container creation finishes, the solution packages SHALL be restored, the frontend packages SHALL be installed, the Dapr runtime SHALL be initialised, and the headless Chromium browser for Playwright SHALL be installed, without further commands from the developer.

#### Scenario: Dapr prerequisite check passes
- **WHEN** the AppHost starts in a freshly created container
- **THEN** the AppHost's Dapr prerequisite check passes, because the Dapr CLI is on `PATH` and the Dapr runtime is initialised

#### Scenario: Frontend packages present
- **WHEN** creation finishes
- **THEN** `src/Frontend/node_modules` contains the packages from the frontend lockfile

### Requirement: The whole application runs inside the dev container
Running the AppHost inside the dev container SHALL start every resource of a local run (frontend, gateway, the Users, Games and Events APIs with their Dapr sidecars, the RabbitMQ broker, the Azurite storage emulator and the dev bot), with the containers the app needs running on a Docker engine inside the dev container.

#### Scenario: All resources running
- **WHEN** a developer runs `aspire run` from the AppHost folder inside the container
- **THEN** every resource reaches the running state without manual steps

#### Scenario: Game playable from the host browser
- **WHEN** the developer opens the forwarded frontend endpoint in a browser on the host
- **THEN** they can create a game and play it to the end against the dev bot, with shots arriving in real time over the event stream

### Requirement: Endpoints are reachable from the host
The Aspire dashboard, the frontend, the gateway and the RabbitMQ management UI SHALL be reachable from a browser on the host through the editor's port forwarding, and the dashboard login link printed by the AppHost SHALL open the dashboard.

#### Scenario: Dashboard login link
- **WHEN** the developer opens the dashboard link that `aspire run` prints in the container
- **THEN** the dashboard opens in the host browser, signed in

#### Scenario: Development certificate trusted inside the container
- **WHEN** a tool inside the container calls an HTTPS endpoint of the run
- **THEN** the Aspire development certificate is trusted and the call does not fail certificate validation

### Requirement: Local state survives a container rebuild
Rebuilding the dev container SHALL keep the AppHost user secrets, the Azurite data with its users and games, the downloaded container images, the initialised Dapr runtime, the NuGet and npm caches, the frontend packages, and the change worktrees created inside the container.

#### Scenario: Generated secrets kept
- **WHEN** the developer rebuilds the container and starts the AppHost again
- **THEN** the generated RabbitMQ password and users signing key are the same as before the rebuild, so earlier access tokens stay valid

#### Scenario: Games kept
- **WHEN** the developer rebuilds the container and starts the AppHost again
- **THEN** the users and games stored before the rebuild are still there

### Requirement: Tests run inside the dev container
The backend test suite, including tests that start containers, and the frontend unit tests SHALL pass inside the dev container, with no extra configuration beyond what the container provides.

#### Scenario: Backend tests
- **WHEN** `dotnet test` runs for the solution inside the container
- **THEN** the tests pass, including the Integration category that starts the AppHost with Docker and Dapr

#### Scenario: Frontend tests
- **WHEN** the frontend unit tests run inside the container
- **THEN** they pass

### Requirement: Change worktrees work inside the dev container
A change worktree created inside the container at the location the change git flow uses, next to the repository, SHALL be usable for building, testing and committing, and SHALL survive a rebuild.

#### Scenario: Worktree for a change
- **WHEN** the change git flow creates `../battleship-worktrees/<change>` inside the container
- **THEN** the worktree is created on the container side, builds and commits work from it, and it is still there after a rebuild

### Requirement: Lifecycle scripts are idempotent and explain failures
The scripts that prepare the container on creation and on start SHALL produce the same result when they run again, and SHALL stop with a message naming the step that failed.

#### Scenario: Re-run is harmless
- **WHEN** the creation script runs a second time in a prepared container
- **THEN** it finishes successfully without reinstalling what is already present

#### Scenario: Failure is named
- **WHEN** a preparation step fails, for example because the Docker engine did not start
- **THEN** the script stops and its output names that step

### Requirement: CI keeps the dev container buildable
Every pull request that changes the dev container definition SHALL build the container in CI and run a smoke check inside it that reports the tool versions, builds the solution and runs the unit tests. The dev container features SHALL be kept up to date by automated dependency updates.

#### Scenario: Broken definition caught
- **WHEN** a pull request changes the dev container definition so that the container no longer builds or a tool is missing
- **THEN** the CI check fails

#### Scenario: Feature updates proposed
- **WHEN** a newer version of a dev container feature is released
- **THEN** an automated dependency update proposes it against the development branch

### Requirement: The dev container holds no secrets
The dev container definition and its scripts SHALL NOT contain secrets, tokens or host-specific paths. Git and GitHub credentials SHALL come from the editor's credential forwarding or a sign-in inside the container.

#### Scenario: Definition review
- **WHEN** the dev container files are inspected
- **THEN** they contain no credentials, tokens or absolute host paths
