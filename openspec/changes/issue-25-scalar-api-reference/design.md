# Design

## Context

See `proposal.md` and `specs/api-reference/spec.md`. Today each API's `Program.cs` repeats the same two lines:
- `builder.Services.AddOpenApi()` (Players `Program.cs:12`, Games `:8`, Events `:10`)
- `app.MapOpenApi()` inside an `IsDevelopment()` block (Players `:28`, Games `:20`, Events `:25`)

The document serves at `/openapi/v1.json`. Bearer authentication comes from `AddBattleshipAuthentication()` in ServiceDefaults (JWT bearer, scheme name `Bearer`). The Events API maps `POST /integration-events` (the Dapr delivery endpoint) with `WithTopic`. That endpoint and the `MapSubscribeHandler()` route are infrastructure. Health endpoints come from `MapDefaultEndpoints()` and are not in the document. `GatewayRoutes.ReservedPrefixes` already contains `openapi`.

## Goals / Non-Goals

**Goals:**
- One place that decides how every API documents itself.
- A reference that works with the existing JWT auth without extra setup.

**Non-Goals:**
- An aggregated reference behind the gateway, or one shared page for all modules.
- Changing endpoint names, summaries or response metadata. They are already present and are only displayed.
- Removing the Games `/weatherforecast` sample. Issue #13 does that, and until then it shows up in the Games reference.

## Decisions

### 1. A shared ServiceDefaults extension, not three copies

ServiceDefaults gains two extensions, `builder.AddApiReference()` and `app.MapApiReference()`:
- **`AddApiReference()`** calls `AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>())`.
- **`MapApiReference()`** maps `MapOpenApi()` and `MapScalarApiReference(...)` only when `app.Environment.IsDevelopment()`.

Each `Program.cs` swaps its two OpenAPI lines for these calls. `Scalar.AspNetCore` is referenced once, in ServiceDefaults, which already has the ASP.NET Core framework reference.

- **Alternative: add the package to each `*.Api.csproj`.** This drifts the same way the test packages did, and a new API could forget it.

### 2. Bearer scheme through a document transformer

`BearerSecuritySchemeTransformer` checks `IAuthenticationSchemeProvider` for the `Bearer` scheme. When the scheme exists, it adds an HTTP `bearer` security scheme (format JWT) to the document's components. It adds a security requirement only to operations whose endpoint metadata carries `IAuthorizeData` and no `IAllowAnonymous`.

That matches the existing groups:
- `/users` requires authorization.
- `/users/tokens` allows anonymous access.

This follows the pattern documented for `Microsoft.AspNetCore.OpenApi` in .NET 10. Scalar is configured to prefer that scheme, through `AddPreferredSecuritySchemes("Bearer")` or the equivalent in the installed version, so the auth panel is ready to use.

- **Alternative: a global security requirement on every operation.** It would wrongly mark the anonymous token endpoints as needing a token.

### 3. Hide infrastructure endpoints with `ExcludeFromDescription()`

- The `/integration-events` group gets `.ExcludeFromDescription()`.
- `MapSubscribeHandler()` returns an endpoint convention builder, and it gets `.ExcludeFromDescription()` as well, if the Dapr route appears in the document. Implementation verifies that.

Exclusion is preferred over an "Internal" tag. The spec says clients should not see these endpoints at all, and an "Internal" group would still invite people to call them.

### 4. Dashboard link per API resource

In `AppHost.cs`, each `AddProject<...Api>` gets a dashboard URL pointing at its reference path on the `http` endpoint, labelled "API reference". This uses Aspire's `WithUrlForEndpoint` / `WithUrls` callback. A small helper keeps the three calls identical. The reference path is a shared constant in `AspireConstants` (already linked into the AppHost), so the AppHost and the APIs cannot disagree.

### 5. Reserve `scalar` in the gateway

`GatewayRoutes.ReservedPrefixes` adds `scalar`, next to the existing `openapi`. `GatewayRoutesTests` gains a case for it. The gateway already routes only the declared prefixes, so this is defence in depth that keeps a future module from claiming the path.

## Risks / Trade-offs

- [The Scalar API surface changes between major versions (the 1.x to 2.x option names)] → Pin an exact current version. Settle the option method names against that version during implementation. The tests assert behaviour (the page answers 200 in Development and 404 otherwise), not option names.
- [The reference shows the template `/weatherforecast` until #13 lands] → Accepted. It disappears with #13, and the Games reference still proves the wiring.
- [A document transformer that misreads the authorization metadata would mark operations wrongly] → A unit test runs the transformer on the Players document and asserts `GET /users/me` is secured and `POST /users/tokens` is not.

## Open Questions

- The current `Scalar.AspNetCore` version and its exact option names for preferred security schemes and the title. These are checked at implementation time and change neither the approach nor the tasks.
