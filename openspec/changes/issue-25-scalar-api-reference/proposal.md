# Proposal

> Tracks GitHub issue [hexmasternl/battleship#25](https://github.com/hexmasternl/battleship/issues/25).

## Why

The *Minimal APIs over controllers* ADR makes the built-in OpenAPI document plus Scalar the standard API reference UI. The Players, Games and Events APIs each publish an OpenAPI document but have no reference UI, so developers cannot browse or try the endpoints without hand-writing requests. In an Aspire demo, each API's dashboard entry should open its reference directly.

## What Changes

- Every API serves an interactive Scalar API reference next to its OpenAPI document, only in Development, through one shared ServiceDefaults call. The per-API `AddOpenApi()` / `MapOpenApi()` lines are replaced by that call.
- The OpenAPI documents describe the bearer-token scheme, so protected endpoints can be called from the reference after pasting an access token.
- Internal endpoints, such as the Dapr delivery endpoint `POST /integration-events`, are left out of the documents. The template `/weatherforecast` sample is out of scope here; #13 removes it.
- The AppHost adds an "API reference" link to each API resource on the Aspire dashboard.
- The README dashboard tour mentions the API reference.

## Capabilities

### New Capabilities

- `api-reference`: the development-time API reference each backend API serves. It covers what the document lists, how to reach it from the dashboard, authentication in the UI, and that it is unavailable outside Development.

### Modified Capabilities

None. `local-gateway` already requires that internal endpoints, `openapi` included, are not routed through the gateway. The reference is served on each API's own endpoint, so that requirement is unchanged.

## Impact

- **Code:**
  - `HexMaster.Battleship.Aspire.ServiceDefaults`: a new `Scalar.AspNetCore` package reference and a shared OpenAPI/reference registration with a bearer security scheme transformer.
  - `Players.Api`, `Games.Api`, `Events.Api` `Program.cs`: switch to the shared call.
  - `IntegrationEventEndpoints`: excluded from the description.
  - `AppHost.cs`: dashboard URLs for each API.
- **Gateway:** `scalar` is added to the reserved prefixes in `GatewayRoutes.cs`, so no module can claim it.
- **Dependencies:** `Scalar.AspNetCore` (MIT).
- **APIs:** no contract changes. The documents only gain the security scheme and lose the internal endpoint.
- **Docs:** README.
