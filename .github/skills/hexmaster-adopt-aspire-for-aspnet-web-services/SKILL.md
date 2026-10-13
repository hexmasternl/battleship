---
name: hexmaster-adopt-aspire-for-aspnet-web-services
description: Applies when adding or changing a web-facing ASP.NET service, wiring a database, cache, message broker or other dependency, editing the Aspire AppHost or ServiceDefaults, or when tempted to hand-write telemetry, health checks, resilience or service discovery. Routes all of that through .NET Aspire.
metadata:
  source: hexmaster-coding-standards
  document: adopt-aspire-for-aspnet-web-services
  status: accepted
---

# HexMaster ADR 0003: Orchestrate web services through .NET Aspire

Strong recommendation (not a mandate) for every web-facing ASP.NET service: HTTP APIs, web
apps, Blazor, gRPC. Domain-only libraries, console tools and test harnesses may opt out.

## Rules

- Aspire orchestration projects live under `src/Aspire/`:
  `Company.Product.Aspire.AppHost` and `Company.Product.Aspire.ServiceDefaults`.
- Every module `.Api` project calls `builder.AddServiceDefaults()` before `builder.Build()` and
  `app.MapDefaultEndpoints()` after it. That is where telemetry, health checks (`/health`,
  `/alive`), resilience defaults and service discovery come from.
- The AppHost references every module API project and declares every infrastructure resource
  (database, broker, cache). Dependencies are attached with `.WithReference(...)` and ordered
  with `.WaitFor(...)`.
- Service endpoints come from the environment variables Aspire generates. Never hardcode a
  URL or connection string to another service.
- Use Aspire integrations and OpenTelemetry exporters before writing a custom equivalent for
  telemetry, health checks, retries, timeouts or circuit breakers.
- Minimum adoption set: AppHost with service defaults, logs/metrics/traces exported, health
  check surface per service, resilience defaults on outbound calls, discovery variables instead
  of hardcoded endpoints.
- Adoption is incremental: start with AppHost plus service defaults, add integrations as
  needed. Any deviation from the defaults is documented in an ADR that references ADR 0003.
- Aspire integration commits are tagged `feat: adopt aspire (ADR-0003)`.

## In this repository

`src/Aspire/HexMaster.Battleship.Aspire/` holds the AppHost and ServiceDefaults. The AppHost
registers the Games and Users APIs. When a change adds a resource such as RabbitMQ, Dapr or a
database, declare it in the AppHost and reference it from the API project rather than
configuring it in appsettings only.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `adopt-aspire-for-aspnet-web-services`.
