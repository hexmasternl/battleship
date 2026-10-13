# Proposal

> Tracks GitHub issue [hexmasternl/battleship#24](https://github.com/hexmasternl/battleship/issues/24).

## Why

ADR 0008 requires every backend service to emit OpenTelemetry traces, metrics and correlated structured logs for its use cases. Today only the Aspire template defaults run. As a result, a request disappears inside an API, the trace breaks at the Dapr pub/sub hop, no business metric exists, and logs are ad hoc. That is a gap against the house standard. It is also a missed opportunity in a repository whose purpose is to show off Aspire, where the dashboard should tell the whole story.

## What Changes

- **Naming convention.** Every backend module gets an `Observability/` folder with one `ActivitySource` and one metrics class built on `IMeterFactory`. All names fall under the `HexMaster.Battleship.*` prefix, so ServiceDefaults can register every source and meter once.
- **Handler decorator.** An instrumentation decorator wraps every `ICommandHandler` and `IQueryHandler`. It opens one activity per use case, records `Ok` or `Error` with the exception, and records the handler duration. Players' handlers get this now, and future Games handlers inherit it.
- **Business metrics:**
  - Players: tokens issued, refreshed and refused (by reason).
  - Integration events: published (by type) and publish failures.
  - Events: active connections, events delivered, dropped and replayed, and heartbeats written.
- **Publishing.** Each integration event publish gets a producer activity. gRPC client instrumentation is enabled, so the trace context travels through the Dapr sidecar into the Events API.
- **Dapr sidecars.** The AppHost configures the sidecars to export their spans to the Aspire dashboard. One trace then covers publisher, sidecar, broker hop, Events API and SSE delivery.
- **Logging.** Ad-hoc log calls become source-generated `LoggerMessage` definitions. The OpenTelemetry logger keeps parsed state values. A redaction rule keeps tokens, secrets and display names out of logs, tags and metrics.
- **Tests.** Telemetry tests use an `ActivityListener` and a metric collector.
- **Documentation.** The README gets an observability section.

## Capabilities

### New Capabilities

- `backend-observability`: the telemetry every backend service emits. It covers trace coverage of use cases and the event pipeline, the business metrics with their names and units, log structure and correlation, data hygiene rules, and vendor-neutral export to the Aspire dashboard.

### Modified Capabilities

None. Messaging, integration-event and real-time behaviour stay the same; this change only makes them observable.

## Impact

- **Code:**
  - `HexMaster.Battleship.Aspire.ServiceDefaults`: OpenTelemetry pipeline (wildcard source and meter registration, gRPC client instrumentation, logging options).
  - `HexMaster.Battleship.Core`: handler decorator and its registration helper.
  - `HexMaster.Battleship.Players`, `.Games`, `.Events`, `.IntegrationEvents`: a new `Observability/` folder in each, plus calls from the existing handlers and components.
  - `AppHost.cs`: Dapr sidecar tracing configuration.
- **Dependencies:** `OpenTelemetry.Instrumentation.GrpcNetClient` in ServiceDefaults (a pre-release package), and `Microsoft.Extensions.Diagnostics.Testing` in the test projects (for `MetricCollector<T>`). No vendor SDKs.
- **APIs:** no endpoint or contract changes.
- **Docs:** a README observability section.
