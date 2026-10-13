---
name: hexmaster-minimal-api-endpoint-organization
description: Applies when adding, moving or reviewing Minimal API endpoints, creating an Endpoints file, naming a route group or its extension method, or when Program.cs starts accumulating MapGet/MapPost calls. One file per base route with a static Map{Feature}Endpoints extension over a route group.
metadata:
  source: hexmaster-coding-standards
  document: minimal-api-endpoint-organization
  status: accepted
---

# HexMaster structure: Minimal API endpoint organization

## Rules

- Each base route (`/orders`, `/users`, `/games`) gets its own file in the API project's
  `Endpoints/` folder, named `{Feature}Endpoints.cs` (PascalCase, plural for resources).
- The file exposes one static extension method
  `Map{Feature}Endpoints(this IEndpointRouteBuilder app)` that creates the group with
  `app.MapGroup("/route").WithTags("...").WithOpenApi()`, maps every endpoint on the group and
  returns `IEndpointRouteBuilder` for chaining.
- `Program.cs` only composes: it registers services and calls each `Map...Endpoints()` method.
  Never define endpoints directly in `Program.cs`.
- Endpoint lambdas or private static methods stay thin: bind, dispatch to the injected
  handler, map the result with `Results.*`. No business logic.
- Apply cross-cutting concerns at group level: `.AddEndpointFilter<T>()`,
  `.RequireAuthorization()`, rate limiting. Give endpoints `.WithName(...)` and `.Produces<T>()`
  metadata for OpenAPI.
- Non-resource routes follow the same pattern (`HealthEndpoints.cs`, `AdminEndpoints.cs`,
  `WebhookEndpoints.cs`).
- Nested sub-resources (`/orders/{id}/items`) stay in the parent file while small; split into
  their own file once they grow.
- Do not mix unrelated routes in one method and keep group nesting shallow.
- Integration tests use `WebApplicationFactory<TProgram>`, call the mapping methods
  explicitly and mock handlers.

## In this repository

Create `Endpoints/UserEndpoints.cs` in `HexMaster.Battleship.Users.Api` and
`Endpoints/GameEndpoints.cs` in `HexMaster.Battleship.Games.Api`, and remove the template
weather forecast endpoint from `Program.cs` when the first real endpoint lands.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `minimal-api-endpoint-organization`.
