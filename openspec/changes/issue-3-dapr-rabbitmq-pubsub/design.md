# Design

## Context

See proposal.md for motivation. Observed state:

- `src/Aspire/.../HexMaster.Battleship.Aspire.AppHost/AppHost.cs` is the generated two-line host: `AddProject` for `HexMaster_Battleship_Games_Api` and `HexMaster_Battleship_Players_Api`, then `Build().Run()`. The project uses `Aspire.AppHost.Sdk/13.6.1` on `net10.0` with `AspireUseCliBundle` enabled and a user-secrets id.
- `HexMaster.Battleship.Aspire.ServiceDefaults/Extensions.cs` provides `AddServiceDefaults()` (OpenTelemetry, health checks, service discovery, resilient HTTP clients) and `MapDefaultEndpoints()`. Every API calls both.
- Both `Program.cs` files are the minimal-API template with a `/weatherforecast` endpoint, `AddOpenApi()` and `UseHttpsRedirection()`. No controllers, no authentication, no messaging packages.
- Test projects are xUnit 2.9 with `Microsoft.NET.Test.Sdk`; the Games test project has a single template test.
- Aspire RabbitMQ hosting is documented at aspire.dev: package `Aspire.Hosting.RabbitMQ`, `builder.AddRabbitMQ("messaging")` with optional username and password parameters (a password parameter is generated when none is given), `.WithManagementPlugin()` for the management UI, and `.WithReference(rabbitmq)` on consumers. The client package is `Aspire.RabbitMQ.Client`, which this change does not need: the services talk to RabbitMQ only through Dapr.
- Dapr hosting for Aspire lives in the Community Toolkit (`CommunityToolkit.Aspire.Hosting.Dapr`). It models sidecars and components as resources and injects `DAPR_HTTP_ENDPOINT` and `DAPR_GRPC_ENDPOINT` into the application. The toolkit is tested against the Dapr runtime its release notes name (1.15 at the time of writing) and requires the Dapr CLI and `dapr init` on the developer machine.

## Goals / Non-Goals

**Goals:**
- One broker and one Dapr component modelled in the AppHost, shared by every API, with no connection string in any repository file.
- Every API gets Dapr publish and subscribe for free through `AddServiceDefaults()` plus two lines in its pipeline.
- A proven path: the smoke test runs the real AppHost, the real sidecars and the real broker.

**Non-Goals:**
- Integration event contracts, envelopes or topic naming for the game domain. The smoke test uses a throwaway topic and payload; issue #4 defines the envelope.
- Deployment of RabbitMQ or Dapr outside the local run. Azure Container Apps hosting is a separate change.
- Dapr state stores, bindings, secrets or service invocation.

## Decisions

**Dapr through the Community Toolkit hosting integration, not raw `dapr run`.** The AppHost adds `CommunityToolkit.Aspire.Hosting.Dapr`, calls the toolkit's Dapr registration on the builder, creates the pub/sub component with the toolkit's pub/sub factory (name `pubsub`) and attaches a sidecar to each `AddProject` call with the component referenced. The implementer verifies the exact extension method names (`AddDapr`, `AddDaprPubSub`, `WithDaprSidecar`, `WithReference` on the component) against the toolkit version chosen, which must be the latest release compatible with Aspire 13.6; the names above are the ones the toolkit has used in its 9.x releases. Alternative considered: a hand-written Dapr components directory with a `pubsub.yaml` pointing at `localhost:5672` and sidecars started by a script; rejected because the broker port is allocated by Aspire and the sidecars would be outside the dashboard, logs and lifecycle.

**RabbitMQ bound to the component through the resource reference.** The toolkit can derive the RabbitMQ component metadata (`pubsub.rabbitmq`, `connectionString` or host/port/credentials) from an Aspire RabbitMQ resource passed with `WithReference`. If the chosen toolkit version does not support referencing the RabbitMQ resource directly, the fallback is a toolkit component whose metadata values are Aspire reference expressions of the `messaging` resource's endpoint and parameters, so that the spec's "no hand-maintained connection string" requirement still holds. Either way, the component manifest is produced from the resource graph at run time, never committed.

**Broker credentials as AppHost parameters.** `AddRabbitMQ("messaging")` is given a username parameter (`rabbitmq-username`, default `guest`) and a password parameter (`rabbitmq-password`) that Aspire generates and stores in user secrets on first run. The management plugin is enabled without a fixed port so parallel runs do not collide; the dashboard shows the port.

**Dapr client registered in ServiceDefaults.** A new `AddDaprMessaging()` extension in ServiceDefaults (called from `AddServiceDefaults()`) registers the Dapr client from `Dapr.AspNetCore` (`AddDaprClient()`), which reads the sidecar endpoints from the environment the AppHost injects. ServiceDefaults gains the `Dapr.AspNetCore` package reference so each API gets `DaprClient`, `[Topic]` and `MapSubscribeHandler()` without adding the package itself. Each API's `Program.cs` adds `UseCloudEvents()` before routing and `MapSubscribeHandler()` after `MapDefaultEndpoints()`. Alternative considered: registering the client per API; rejected because the Events API (issue #4) and any later service would repeat the same three lines.

**Smoke test shape.** The Players API exposes `POST /smoke/publish` which publishes `{ "id": "<guid>" }` to topic `smoke` on `pubsub`; the Games API exposes a `[Topic("pubsub", "smoke")]` handler at `POST /smoke/received` that records the id in an in-memory set and `GET /smoke/received/{id}` that returns `200` or `404`. The test in `HexMaster.Battleship.Games.Tests` uses `Aspire.Hosting.Testing` to start the AppHost, waits for both APIs and the broker to be healthy, posts to the publish endpoint and polls the received endpoint for up to thirty seconds. The two smoke endpoints are kept behind `IsDevelopment()` and removed by the change that introduces the first real integration event. Alternative considered: a unit test with a mocked `DaprClient`; rejected because it would not prove the sidecar, the component or the broker.

**Fail fast when Dapr is missing.** The toolkit already fails the sidecar resource when the `daprd` binary cannot be found; the AppHost adds a startup check that runs `dapr --version` and verifies the runtime directory from `dapr init` exists, and throws with a message that names the missing step. This keeps the dashboard from showing endlessly restarting sidecars.

**Documentation.** `README.md` gets a "Running locally" section: prerequisites (Docker Desktop, .NET 10 SDK, Aspire CLI, Dapr CLI, `dapr init`), the start command (`aspire run` from `src/Aspire/HexMaster.Battleship.Aspire`), and where to find the RabbitMQ management UI and the Dapr sidecar logs on the dashboard.

## Risks / Trade-offs

- [The Community Toolkit Dapr package lags behind Aspire 13.6 or renames its API] -> the first task pins the package version and verifies the calls compile; if no compatible release exists the implementer stops and reports instead of hand-rolling sidecars.
- [The toolkit cannot reference the RabbitMQ resource directly for the component metadata] -> the fallback in the Decisions section keeps the metadata as reference expressions so the spec still holds; the task notes which path was taken.
- [`dapr init` installs its own Redis and Zipkin containers that are not used here] -> documented as expected; they do not conflict with the Aspire RabbitMQ container.
- [RabbitMQ container pull slows the first run and the smoke test] -> the smoke test waits for the resource health states with a generous timeout and is marked as an integration test (trait `Integration`) so unit runs can filter it out.
- [Two sidecars plus the broker increase local resource usage] -> acceptable for a two-service solution; the management plugin can be dropped later if memory becomes a problem.

## Resolved during apply

**Package versions (task 1.1, 2026-10-13).**

| Package | Version | Note |
|---|---|---|
| `Aspire.Hosting.RabbitMQ` | 13.6.1 | matches `Aspire.AppHost.Sdk/13.6.1` |
| `CommunityToolkit.Aspire.Hosting.Dapr` | 13.6.0-preview.1.261001-0243 | the toolkit's tagged preview for Aspire 13.6 (depends on `Aspire.Hosting` 13.6.0). The latest stable toolkit release is 13.0.0, which pins `Aspire.Hosting` 13.0.0 and does not fit the 13.6.1 SDK. The `13.6.1-beta.*` builds are unreleased CI output and were not chosen. |
| `Dapr.AspNetCore` | 1.18.10 | latest stable; the machine runs Dapr CLI 1.18.2 / runtime 1.17.4 |
| `Aspire.Hosting.Testing` | 13.6.1 | matches the SDK |

**Verified toolkit API (13.6.0-preview.1).** The public surface, taken from the package's XML documentation:

- `builder.AddDapr(Action<DaprOptions>? configure = null)` registers the lifecycle hook that starts the sidecars. `DaprOptions` has `DaprPath`, `EnableTelemetry` and `PublishingConfigurationAction`.
- `builder.AddDaprComponent(name, type, DaprComponentOptions? options = null)` adds a component with a concrete Dapr type (for example `pubsub.rabbitmq`). `AddDaprPubSub(name)` and `AddDaprStateStore(name)` add the *generic* `pubsub` / `state` building blocks, which at run time resolve to `<AppHost>/.dapr/components/<name>.yaml`, then `~/.dapr/components/<name>.yaml` (the Redis one that `dapr init` writes), then an in-memory component. That is why this change uses `AddDaprComponent` with the concrete type and not `AddDaprPubSub`.
- `component.WithMetadata(name, string)` adds static metadata; `component.WithMetadata(name, IValueProvider)` adds metadata resolved at run time (an `EndpointReference`, `ReferenceExpression` or connection-string expression); `component.WithMetadata(name, ParameterResource)` adds a parameter (secret parameters become a `secretKeyRef`).
- `resource.WithDaprSidecar()`, `WithDaprSidecar(string appId)`, `WithDaprSidecar(DaprSidecarOptions)` and `WithDaprSidecar(Action<IResourceBuilder<IDaprSidecarResource>>)` attach a sidecar; `resource.WithReference(component)` and `sidecar.WithReference(component)` make the sidecar load the component.
- There is **no** overload that takes a RabbitMQ resource, so the fallback path from the Decisions section is the one taken: the `pubsub` component gets its `connectionString` metadata from the `messaging` resource's `ConnectionStringExpression` (`amqp://<user>:<password>@<host>:<port>`). The toolkit writes that value to an environment variable of the sidecar process (`PUBSUB_CONNECTIONSTRING`) and emits a `secretKeyRef` into the generated component manifest backed by an auto-generated `secretstores.local.env` component named `secretstore`, so neither the manifest nor any repository file contains the host, port or password.
- The lifecycle hook writes the generated manifests to a temporary directory (`aspire-dapr.*`), runs each sidecar as a `dapr run` executable resource named `<sidecar>-cli` with `--resources-path` pointing there, and sets `DAPR_HTTP_PORT`, `DAPR_GRPC_PORT`, `DAPR_HTTP_ENDPOINT` and `DAPR_GRPC_ENDPOINT` on the application resource. It locates the Dapr CLI at `<SystemDrive>\dapr\dapr.exe` (Windows) or `~/dapr/dapr`, `/usr/local/bin/dapr`, `/usr/bin/dapr` (other), then on `PATH`, and throws `Unable to locate the Dapr CLI.` only when a sidecar is about to start. It does not check that `dapr init` has been run, hence the AppHost's own startup check (task 2.3).

**Verified `Aspire.Hosting.RabbitMQ` API (13.6.1).** `AddRabbitMQ(name, IResourceBuilder<ParameterResource>? userName = null, IResourceBuilder<ParameterResource>? password = null, int? port = null)`, `WithManagementPlugin(int? port = null)`, and on the resource `PrimaryEndpoint`, `ManagementEndpoint`, `UserNameParameter`, `PasswordParameter`, `ConnectionStringExpression`.

## Open Questions

- ~~Which exact Community Toolkit Dapr version is current when the change is applied.~~ Resolved above.
