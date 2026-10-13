# Design

## Context

See `proposal.md` (Why) and `specs/backend-observability/spec.md` for the requirements. What the code has today:

- **ServiceDefaults.** `ConfigureOpenTelemetry` (in `HexMaster.Battleship.Aspire.ServiceDefaults/Extensions.cs`) registers ASP.NET Core, `HttpClient` and runtime instrumentation, plus one source named after `ApplicationName`. The OTLP exporter switches on only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, which Aspire does for every project. The logger has `IncludeFormattedMessage` and `IncludeScopes` but not `ParseStateValues`.
- **CQRS.** `ICommandHandler<TCommand, TResult>`, `ICommandHandler<TCommand>` and `IQueryHandler<TQuery, TResult>` live in `HexMaster.Battleship.Core/Cqrs`. Modules register them by hand: `PlayersModuleRegistration` adds three scoped handlers, and endpoints resolve the interface.
- **Publishing.** `DaprIntegrationEventSender` publishes through `DaprClient`, which talks gRPC to the sidecar. Grpc.Net.Client runs over `HttpClient`, so the existing `HttpClient` instrumentation probably already injects `traceparent`. That has not been verified, and the span it produces is named after a generic HTTP/2 POST.
- **Events.** `IntegrationEventEndpoints.Receive` receives each message, `EventRouter.Route` routes it, and `EventConnection` writes to a bounded channel. Their logs are inline `LogInformation` / `LogWarning` calls.
- **AppHost.** It runs three Dapr sidecars through the Community Toolkit with `DaprSidecarOptions { AppId = ... }` and nothing more. Without a configuration, `daprd` uses the default from `dapr init`, which samples to a local Zipkin container and never reaches the dashboard.
- **Dependency direction.** ServiceDefaults must not reference module projects: modules reference ServiceDefaults, not the other way round. A pending local edit already removes ServiceDefaults' reference to IntegrationEvents.

## Goals / Non-Goals

**Goals:**
- One convention every module follows, so instrumenting a new module or handler costs almost nothing.
- Traces that run unbroken from the gateway to the SSE delivery in the Aspire dashboard.
- Telemetry that tests can verify without a running collector.

**Non-Goals:**
- Frontend and browser telemetry (a follow-up issue).
- Sampling configuration (the default stays: parent-based, always-on locally), tail sampling, and exemplars.
- Production exporters, alerting and saved dashboards.
- Instrumenting Azurite or RabbitMQ themselves; their client libraries and the sidecar cover the hops.

## Decisions

### 1. Naming convention plus wildcard registration in ServiceDefaults

Every module names its `ActivitySource` and `Meter` after its assembly: `HexMaster.Battleship.Players`, `HexMaster.Battleship.Events`, `HexMaster.Battleship.IntegrationEvents`, `HexMaster.Battleship.Games`, and `HexMaster.Battleship.Core` for the decorator. ServiceDefaults registers them all with `AddSource("HexMaster.Battleship.*")` and `AddMeter("HexMaster.Battleship.*")`. OpenTelemetry .NET supports wildcards for both.

- **Alternative: list every source in ServiceDefaults.** ServiceDefaults would need to reference the modules (wrong direction), or each module would need an `IConfigureOptions` hook that is easy to forget.
- **Alternative: register per API host.** Every host would repeat the same lines.

The wildcard keeps one rule ("name it after your assembly") and costs nothing when a new module arrives.

### 2. Per-module `Observability/` folder

Each module gets a folder holding three things:
- `<Module>Telemetry`: a static class with `public const string Name` and a static `ActivitySource`, plus the attribute-name constants (`player.id`, `token.refusal_reason`, `event.type`, `connection.id`, ...).
- `<Module>Metrics`: a sealed class built on `IMeterFactory` (`meterFactory.Create(<Module>Telemetry.Name)`), registered as a singleton. It exposes intention-revealing methods (`TokensIssued()`, `TokenRefused(reason)`) rather than raw instruments.
- `<Module>Log`: a static partial class with `[LoggerMessage]` definitions. Each has a stable `EventId`, numbered per module in a range of a thousand (Players 1000s, Events 2000s, IntegrationEvents 3000s, Games 4000s, Core 5000s).

This follows the repository note in the ADR 0008 skill. Using `IMeterFactory` instead of a static `Meter` keeps meters scoped to the DI container, which is what makes `MetricCollector<T>` in tests reliable.

### 3. A decorator around the handler interfaces, with no new DI package

`HexMaster.Battleship.Core/Observability` adds three decorators:
- `InstrumentedCommandHandler<TCommand, TResult>`
- `InstrumentedCommandHandler<TCommand>`
- `InstrumentedQueryHandler<TQuery, TResult>`

Each one:
- starts an `Internal` activity named after the use case. The name is the command or query type without its `Command`/`Query` suffix, so `CreateTokens` and `GetCurrentUser`.
- tags `battleship.use_case` and `battleship.use_case.kind` (command or query).
- records `battleship.use_case.duration` (a histogram, unit `s`) with tags `use_case` and `outcome` (`success` or `failure`).
- on success sets the status to `Ok`; on an exception calls `SetStatus(Error, ex.Message)` and `AddException(ex)` before rethrowing.

Registration goes through new helpers:
- `services.AddCommandHandler<TCommand, TResult, THandler>()`
- `services.AddCommandHandler<TCommand, THandler>()`
- `services.AddQueryHandler<TQuery, TResult, THandler>()`

Each helper registers the concrete handler as scoped and the interface as a factory that wraps it. `PlayersModuleRegistration` switches to these helpers, and Games uses them from its first handler on.

- **Alternative: Scrutor's `Decorate`.** It adds a third-party package for one line of value. The house rule asks for a defensible reason, and three small helpers are enough.
- **Alternative: an endpoint filter.** It would miss handlers called from non-HTTP paths, and the activity would sit at the wrong level.

Handlers can still add attributes to `Activity.Current`. Inside a handler, `Activity.Current` is the use-case span, so domain details (for example the refusal reason) are added there instead of in a second span.

### 4. Expected failures versus faults

`TokenRefusedException` is an expected outcome: the request is answered with 401. The decorator still marks the span `Error`, as the spec requires, and the business metric `players.tokens.refused{reason}` counts the refusal by its bounded reason. The reason strings are the five fixed values the handler already throws: `unknown token`, `expired token`, `token used already`, `unknown user`, `revoked user`. The handler maps them to snake_case constants so the tag set stays closed.

### 5. Metric catalogue

Units use UCUM curly-brace annotations (`{token}`, `{event}`, `{connection}`).

| Meter | Instrument | Type | Unit | Tags |
|---|---|---|---|---|
| Core | `battleship.use_case.duration` | Histogram | `s` | `use_case`, `outcome` |
| Players | `players.tokens.issued` | Counter | `{token}` | none |
| Players | `players.tokens.refreshed` | Counter | `{token}` | none |
| Players | `players.tokens.refused` | Counter | `{token}` | `reason` |
| IntegrationEvents | `integration_events.published` | Counter | `{event}` | `event.type` |
| IntegrationEvents | `integration_events.publish_failed` | Counter | `{event}` | `event.type` |
| Events | `events.connections.active` | UpDownCounter | `{connection}` | none |
| Events | `events.delivered` | Counter | `{event}` | `event.type` |
| Events | `events.dropped` | Counter | `{event}` | `reason` (`invalid_envelope`, `connection_closed`, `channel_full`) |
| Events | `events.replayed` | Counter | `{event}` | none |

`event.type` comes from the closed integration-event catalog, so its cardinality stays bounded. Envelopes that fail validation never use their (untrusted) type as a tag; they are counted only under `events.dropped{reason=invalid_envelope}`.

### 6. Producer and consumer spans for the event pipeline

- **Publishing.** `DaprIntegrationEventSender` starts a `Producer` activity named `publish integration-events`, following the messaging semantic conventions:
  - `messaging.system=dapr`
  - `messaging.destination.name=integration-events`
  - `messaging.operation.type=publish`
  - `messaging.message.id=<envelope id>`
  - `event.type`

  `DaprClient` then runs inside that activity.
- **Context propagation.** ServiceDefaults adds `OpenTelemetry.Instrumentation.GrpcNetClient` (`AddGrpcClientInstrumentation()`, today a commented-out line in the template). The gRPC call then gets its own span, and the context propagation is explicit. Dapr copies the incoming `traceparent` into the CloudEvent's `traceparent` and onto the delivery request to the subscriber.
- **Receiving.** `IntegrationEventEndpoints.Receive` runs under the ASP.NET Core server span, which continues the propagated context. It starts a `Consumer` activity named `process integration-events` with `messaging.operation.type=process`, the message id and `event.type`. A validation drop ends that activity with `Error`.
- **Delivery.** `EventRouter.Route` adds one `Internal` child activity, `deliver`, with `event.sequence` and `connection.count`, and an activity event per closed connection it removes. One span per delivery rather than per connection keeps a broadcast to many players from flooding the trace.

### 7. Dapr sidecars export over OTLP to the dashboard

The AppHost writes a Dapr `Configuration` for each run with:
- `tracing.samplingRate: "1"`
- `tracing.otel.endpointAddress` set to the dashboard's OTLP gRPC endpoint, `isSecure: false`, `protocol: grpc`

It passes the file through `DaprSidecarOptions.Config`. The endpoint comes from the AppHost's dashboard OTLP endpoint setting (`ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` / `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL`). The file is generated at run time into the AppHost's temp directory and never committed, which matches how the pubsub component avoids committed connection details.

- **Alternative: the default Zipkin config from `dapr init`.** Its spans never reach the dashboard.
- **Alternative: a committed `dapr-config.yaml`.** It can't know the run's dashboard port.

If the Community Toolkit already exposes a hook that sets the sidecar's OTLP endpoint by the time this is implemented, use the hook instead. That changes the code, not the behaviour the spec describes.

### 8. Logging

- ServiceDefaults sets `ParseStateValues = true` (alongside formatted message and scopes) so named values arrive as separate fields.
- Every existing `Log*` call in Events and in the Players `TokenEndpoints` moves to the module's `<Module>Log` source-generated definitions. The Players handlers gain logs for issued, refreshed and refused tokens at `Information`, without token values or display names.
- Trace and span ids are attached by the OpenTelemetry log exporter from `Activity.Current`; no manual scope is needed.

### 9. Testing

- **Spans.** A shared `ActivityCollector` test helper in each test project wraps an `ActivityListener` filtered to `HexMaster.Battleship.*` and collects stopped activities.
- **Metrics.** Tests use `MetricCollector<T>` from `Microsoft.Extensions.Diagnostics.Testing`, with an `IMeterFactory` taken from a `ServiceCollection` that has `AddMetrics()`.
- **What is covered:**
  - the decorator: Ok and Error status, the exception event, the duration histogram tags.
  - the Players handlers: each metric and the refusal-reason attribute.
  - the sender: the producer span and its attributes, and published and failed counts.
  - the router: the deliver span, delivered, dropped and replayed counts, and the connection gauge.
  - a hygiene test: run a create plus a refresh, then assert that no collected tag or log state value equals the issued tokens or the display name.
- **Dapr export.** This is checked manually through the dashboard and recorded in the PR. An automated test would need a running sidecar.

## Risks / Trade-offs

- [`OpenTelemetry.Instrumentation.GrpcNetClient` is a pre-release package] → It is pinned to an exact version, and the reason is documented in the csproj. If it causes trouble, removing it leaves propagation to the `HttpClient` instrumentation, which still produces a valid, less descriptive span.
- [The wildcard also picks up any future third-party source under `HexMaster.Battleship.*`] → The namespace belongs to this repository, so no third party publishes under it.
- [The dashboard OTLP endpoint setting name could change between Aspire versions] → Fail soft: if the variable is absent, the AppHost logs a warning and starts the sidecars without the tracing config.
- [Error status on expected refusals could look alarming in the dashboard] → This is accepted per the spec. The refusal reason is on the span, and the 401 response status makes the expectation clear.
- [`events.connections.active` is in-process and the service runs as a single instance] → That matches the current single-instance design. If the service is ever scaled out, the metric sums correctly across instances.

## Migration Plan

This only adds telemetry, with no data or contract changes. Rollback is reverting the merge. Local runs pick up the new telemetry on the next `aspire run`, with no developer action, because the sidecar config is generated.

## Open Questions

- The exact Dapr `Configuration` key spelling for the OTLP protocol (`otel.protocol` versus a `grpc`/`http` scheme in the endpoint) depends on the installed Dapr runtime version. Settle it while implementing task 6 by checking the runtime `dapr --version` reports. The approach stays the same either way.
