---
name: hexmaster-minimal-apis-over-controllers
description: Applies when creating an ASP.NET API project, adding or changing an HTTP endpoint, configuring OpenAPI or an API reference UI, or when a controller, [ApiController] or Swashbuckle is about to be introduced. Makes Minimal APIs with built-in OpenAPI and Scalar the default.
metadata:
  source: hexmaster-coding-standards
  document: minimal-apis-over-controllers
  status: accepted
---

# HexMaster ADR 0005: Minimal APIs over controller-based APIs

## Rules

- New ASP.NET API projects use Minimal APIs. Do not add MVC controllers, `[ApiController]` or
  attribute routing to a new project. Existing controller-based projects may stay as they are;
  no forced migration.
- OpenAPI comes from the framework: `builder.Services.AddOpenApi()` and `app.MapOpenApi()`.
  The API UI is Scalar via `app.MapScalarApiReference()` from `Scalar.AspNetCore`. Do not add
  Swashbuckle or Swagger UI.
- Group related endpoints with `app.MapGroup("/route")` and give the group `.WithTags(...)`.
- Endpoint lambdas stay thin and delegate to injected command/query handlers.
- Request and response models are C# records.
- Apply shallow validation at the edge (required fields, format, range) through endpoint
  filters or manual checks and return `400 Bad Request` for malformed input. Business
  invariants are the domain model's job.
- Cross-cutting concerns (logging, telemetry, error handling) are endpoint filters registered
  with `.AddEndpointFilter<T>()`, at group level where they apply to every endpoint.
- Produce responses through the `Results` helpers (`Results.Ok`, `Results.Created`,
  `Results.Accepted`, `Results.NotFound`).
- Controllers are only justified for a large legacy codebase with heavy custom action-filter
  investment or third-party tooling that requires MVC. Verify a Minimal API alternative first.

## In this repository

Both `HexMaster.Battleship.Players.Api` and `HexMaster.Battleship.Games.Api` are Minimal API
hosts that already call `AddOpenApi` and `MapOpenApi`. Replace the template weather forecast
endpoint with grouped endpoints in `Endpoints/` files and add Scalar when the API UI is needed.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `minimal-apis-over-controllers`.
