---
name: hexmaster-adopt-opentelemetry-for-observability
description: Applies when writing or reviewing a command or query handler, a background job, a message consumer, an outbound integration, or any logging, metrics or tracing code in a backend service. Requires OpenTelemetry activities, metrics and correlated structured logs instead of vendor SDKs.
metadata:
  source: hexmaster-coding-standards
  document: adopt-opentelemetry-for-observability
  status: accepted
---

# HexMaster ADR 0008: OpenTelemetry for traces, metrics and logs

Required for every backend service: APIs, workers, background services.

## Rules

- All three pillars go through OpenTelemetry via the `System.Diagnostics` APIs
  (`ActivitySource`, `Meter`). No per-pillar vendor SDKs in code; backends are swapped through
  exporter configuration only.
- Automatic instrumentation must cover inbound HTTP, outbound `HttpClient`, database access and
  message broker operations. With Aspire this comes from `builder.AddServiceDefaults()`.
- Every command/query handler, multi-step operation (3+ steps), background job, non-instrumented
  external call and complex domain operation creates an activity:
  `using var activity = ActivitySource.StartActivity("CreateOrder", ActivityKind.Internal);`
  Tag it with low-cardinality, domain-prefixed attributes (`order.line_count`,
  `payment.status`), set `ActivityStatusCode.Ok` on success and on failure call
  `SetStatus(Error, message)` and `RecordException(ex)` before rethrowing.
- Do not create activities for trivial getters, already-instrumented calls, inner loops or pure
  functions.
- Business operations emit metrics through a per-module metrics class built on
  `IMeterFactory`: counters for operation counts, histograms for durations and values, gauges
  for point-in-time values. Name them `module.thing` with a unit and description
  (`orders.created`, unit `{order}`).
- Never use high-cardinality tags (user ids, emails) on metrics and never put PII, tokens or
  passwords in tags or logs.
- Follow OpenTelemetry semantic conventions for HTTP, database and messaging attributes; prefix
  custom attributes with the domain (`order.`, `user.`, `player.`, `game.`).
- Logs are structured (message templates with named placeholders) and carry TraceId and SpanId;
  configure `AddOpenTelemetry` on logging with formatted message, scopes and parsed state.
- Exporter endpoints, service name and version come from `OTEL_*` environment variables.
- Register each `ActivitySource` name and `Meter` name with the tracing and metrics pipeline
  (Aspire ServiceDefaults does this for sources it knows about; add module sources there).
- A pull request that adds a handler must add its activity; one that adds a business operation
  must add success/failure metrics. Unit tests should verify activities with an
  `ActivityListener`.
- Reduce boilerplate with a decorator around `ICommandHandler`/`IQueryHandler` that opens the
  activity and records status, rather than skipping instrumentation.

## In this repository

Put a module's `ActivitySource` and metrics class under `Observability/` in the module project
(for example `HexMaster.Battleship.Games/Observability/GameMetrics.cs`) and register the source
and meter names in `HexMaster.Battleship.Aspire.ServiceDefaults`.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `adopt-opentelemetry-for-observability`.
